using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Specification;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Availability.Application.Reservations.Commands.SyncReservationStatus;

/// <summary>
/// Paso 7 de la consigna del TP + resolución de TD-1: el usuario consulta el resultado a través de Availability,
/// que consulta a Booking y SINCRONIZA el estado local, para que <c>Seat.IsAvailable</c> (guarda de idempotencia y
/// concurrencia) no quede bloqueado cuando Booking rechaza o vence la reserva.
/// <para>
/// FRONTERA DE CONSISTENCIA EVENTUAL (req. 3.g): si Booking todavía no procesó el mensaje de la cola, no conoce la
/// reserva y se informa <see cref="ReservationStatus.Pending"/>; el resultado definitivo aparece después.
/// </para>
/// <para>
/// Reglas (el estado informado por Booking manda; el handler es idempotente y solo escribe si algo cambia, con un
/// único SaveChanges para solicitud + asiento):
/// <list type="bullet">
/// <item>Booking caído: la excepción técnica sube (503) y NO se escribe nada: caído nunca expira ni libera.</item>
/// <item>Booking no la conoce (404) y la solicitud es <c>Pending</c> con más antigüedad que el timeout de huérfana:
/// se marca <c>Expired</c> y se libera el asiento (cubre la caída entre guardar y publicar).</item>
/// <item><c>Retained</c>/<c>Confirmed</c>: se guarda el estado y se asegura el asiento bloqueado (cubre la reversión
/// tardía de una huérfana que Booking terminó procesando).</item>
/// <item><c>Rejected</c>/<c>Expired</c>: se guarda el estado y se libera el asiento, salvo que otra solicitud local
/// activa (Pending/Retained/Confirmed) lo siga necesitando.</item>
/// </list>
/// </para>
/// LÍMITE: solo reconcilia lo que alguien consulta; la alternativa (worker con consulta batch a Booking) está
/// documentada en docs/PLAN.md, "Deuda técnica".
/// </summary>
public sealed class SyncReservationStatusCommandHandler(
    ISeatRequestRepository requests,
    ISeatRepository seats,
    IBookingClient booking,
    ICurrentUserAccessor currentUser,
    TimeProvider clock,
    IOptions<ReservationSyncOptions> options,
    ILogger<SyncReservationStatusCommandHandler> logger)
    : IRequestHandler<SyncReservationStatusCommand, ReservationStatusResult>
{
    public async Task<ReservationStatusResult> Handle(SyncReservationStatusCommand command, CancellationToken ct)
    {
        // Solo se responde por reservas originadas aquí: no se reenvían ids arbitrarios a Booking.
        var reservationId = command.ReservationId;
        var local = await requests.FirstOrDefaultAsync(SeatRequestSpecification.ByReservationId(reservationId), ct)
                    ?? throw new ReservationNotFoundException(reservationId);

        // Solo el dueño ve su reserva; para los demás "no existe" (no se revela la existencia de reservas ajenas).
        if (local.UserId != currentUser.UserId) throw new ReservationNotFoundException(reservationId);

        var info = await booking.GetStatusAsync(reservationId, ct); // BookingUnavailableException sube sin escribir nada

        var (status, message, expiresAtUtc) = info is null
            ? await ReconcileUnknownToBookingAsync(local, ct)
            : await ReconcileKnownToBookingAsync(local, info.Status, info.ExpiresAtUtc, ct);

        return new ReservationStatusResult(reservationId, status, message, local.SeatNumber, expiresAtUtc);
    }

    private async Task<(ReservationStatus, string, DateTime?)> ReconcileUnknownToBookingAsync(
        SeatRequest local, CancellationToken ct)
    {
        var age = clock.GetUtcNow().UtcDateTime - local.RequestedAtUtc;
        var isOrphan = local.Status == ReservationStatus.Pending
                       && age > TimeSpan.FromMinutes(options.Value.PendingTimeoutMinutes);

        if (!isOrphan)
            return (local.Status, MessageFor(local.Status, orphan: false), null);

        logger.LogWarning(
            "Reserva {ReservationId} huérfana ({Age:F0} min sin que Booking la conozca): se marca Expired.",
            local.ReservationId, age.TotalMinutes);
        await ApplyAsync(local, ReservationStatus.Expired, ct);
        return (ReservationStatus.Expired, MessageFor(ReservationStatus.Expired, orphan: true), null);
    }

    private async Task<(ReservationStatus, string, DateTime?)> ReconcileKnownToBookingAsync(
        SeatRequest local, ReservationStatus bookingStatus, DateTime? expiresAtUtc, CancellationToken ct)
    {
        await ApplyAsync(local, bookingStatus, ct);
        return (bookingStatus, MessageFor(bookingStatus, orphan: false),
            bookingStatus == ReservationStatus.Retained ? expiresAtUtc : null);
    }

    /// <summary>Lleva solicitud y asiento al estado que corresponde; escribe (una sola vez) únicamente si algo cambió.</summary>
    private async Task ApplyAsync(SeatRequest local, ReservationStatus target, CancellationToken ct)
    {
        var changed = false;

        if (local.Status != target)
        {
            local.Status = target;
            requests.Update(local);
            changed = true;
        }

        var seat = await seats.FirstOrDefaultAsync(SeatSpecification.BySeatId(local.SeatId), ct);
        if (seat is null)
        {
            logger.LogWarning(
                "Reserva {ReservationId}: el asiento {SeatId} no existe localmente; se omite la liberación.",
                local.ReservationId, local.SeatId);
        }
        else
        {
            var shouldBeAvailable = await ShouldSeatBeAvailableAsync(local, target, ct);
            if (shouldBeAvailable is { } available && seat.IsAvailable != available)
            {
                seat.IsAvailable = available;
                seats.Update(seat);
                changed = true;
                logger.LogInformation("Reserva {ReservationId} → {Status}: asiento {SeatNumber} {Action}.",
                    local.ReservationId, target, seat.SeatNumber, available ? "liberado" : "bloqueado");
            }
        }

        if (changed) await requests.SaveChangesAsync(ct);
    }

    /// <summary>
    /// <c>false</c> si la reserva retiene el asiento (Retained/Confirmed); <c>true</c> si ya no lo retiene y ninguna
    /// otra solicitud activa lo necesita; <c>null</c> si no corresponde tocar el flag (Pending: lo bloqueó el POST).
    /// </summary>
    private async Task<bool?> ShouldSeatBeAvailableAsync(SeatRequest local, ReservationStatus target, CancellationToken ct)
    {
        if (target == ReservationStatus.Pending) return null;
        if (target is ReservationStatus.Retained or ReservationStatus.Confirmed) return false;

        var otherActive = await requests.FirstOrDefaultAsync(
            SeatRequestSpecification.ActiveForSeat(local.SeatId, local.ReservationId), ct);
        return otherActive is null;
    }

    private static string MessageFor(ReservationStatus status, bool orphan) => status switch
    {
        ReservationStatus.Pending => "La reserva aún no fue procesada por Booking",
        ReservationStatus.Retained => "Reserva retenida, pendiente de pago",
        ReservationStatus.Confirmed => "Reserva confirmada",
        ReservationStatus.Rejected => "Reserva rechazada",
        ReservationStatus.Expired when orphan => "La reserva venció: Booking no llegó a procesarla",
        ReservationStatus.Expired => "La reserva venció",
        _ => "Estado de reserva desconocido"
    };
}
