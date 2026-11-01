using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;
using MediatR;

namespace Availability.Application.Reservations.Queries.GetReservationStatus;

/// <summary>
/// Paso 7 de la consigna del TP: el usuario consulta el resultado a través de Availability, que consulta a Booking.
/// <para>
/// FRONTERA DE CONSISTENCIA EVENTUAL (req. 3.g): si Booking todavía no procesó el mensaje de la cola, no conoce la
/// reserva (404) y se informa <see cref="ReservationStatus.Pending"/>; el resultado definitivo aparece después.
/// </para>
/// <para>
/// Es una query PURA (CQRS): no escribe nada. En particular NO modifica <c>SeatRequest.Status</c> ni devuelve
/// <c>Seat.IsAvailable</c> a <c>true</c> (ese flujo es la deuda técnica TD-1 de docs/PLAN.md).
/// Un fallo al consultar a Booking sube como <see cref="Exceptions.BookingUnavailableException"/> (→ 503): el
/// estado es desconocido y no se disimula devolviendo "Pending".
/// </para>
/// </summary>
public sealed class GetReservationStatusQueryHandler(ISeatRequestRepository requests, IBookingClient booking)
    : IRequestHandler<GetReservationStatusQuery, ReservationStatusResult>
{
    public async Task<ReservationStatusResult> Handle(GetReservationStatusQuery query, CancellationToken ct)
    {
        // Solo se responde por reservas originadas aquí: no se reenvían ids arbitrarios a Booking.
        var reservationId = query.ReservationId;
        var local = await requests.FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                    ?? throw new ReservationNotFoundException(reservationId);

        var info = await booking.GetStatusAsync(reservationId, ct);

        if (info is null)
            return new ReservationStatusResult(reservationId, ReservationStatus.Pending, MessageFor(ReservationStatus.Pending),
                local.SeatNumber, null);

        return new ReservationStatusResult(
            reservationId,
            info.Status,
            MessageFor(info.Status),
            local.SeatNumber,
            info.Status == ReservationStatus.Retained ? info.ExpiresAtUtc : null);
    }

    private static string MessageFor(ReservationStatus status) => status switch
    {
        ReservationStatus.Pending => "La reserva aún no fue procesada por Booking",
        ReservationStatus.Retained => "Reserva retenida, pendiente de pago",
        ReservationStatus.Confirmed => "Reserva confirmada",
        ReservationStatus.Rejected => "Reserva rechazada",
        ReservationStatus.Expired => "La reserva venció",
        _ => "Estado de reserva desconocido"
    };
}
