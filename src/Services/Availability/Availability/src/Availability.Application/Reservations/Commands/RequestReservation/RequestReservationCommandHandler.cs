using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Exceptions;
using Availability.Application.Messaging;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using SeatsReservation.Contracts.Events;

namespace Availability.Application.Reservations.Commands.RequestReservation;

/// <summary>
/// Paso 3 de la consigna del TP: verifica que el asiento exista, registra la solicitud con un id único y la
/// publica en el broker. La orquestación vive aquí (no en las entidades, que son anémicas, req. 3.a.ii).
/// <para>
/// FRONTERA DE CONSISTENCIA EVENTUAL (req. 3.g): cuando este handler termina, el usuario ya recibe "recibida"
/// (202) pero Booking todavía no decidió; el resultado final se consulta después (paso 7).
/// </para>
/// <para>
/// <c>Seat.IsAvailable</c> es un GUARDA LOCAL de idempotencia y concurrencia ante la consistencia eventual con
/// Booking: al reservar se marca en <c>false</c> y un nuevo pedido para ese asiento se rechaza
/// (<see cref="SeatUnavailableException"/>) sin llegar al broker. La exclusión mutua definitiva sigue siendo de Booking.
/// DEUDA TÉCNICA: falta definir el flujo que devuelve el flag a <c>true</c> (p. ej. al saber que Booking
/// rechazó o venció la reserva) y volver atómico el chequeo (ver docs/PLAN.md, "Deuda técnica").
/// </para>
/// <para>
/// Orden: guardar (Pending, asiento no disponible) → publicar. Si la publicación falla se compensa: se elimina
/// el registro y se restaura el asiento a disponible, de modo que no queda una solicitud Pending huérfana ni el
/// asiento bloqueado; la excepción técnica sube para responder 503 y nunca 202.
/// (Un outbox transaccional sería la mejora a futuro; se evita aquí por simplicidad.)
/// </para>
/// </summary>
public sealed class RequestReservationCommandHandler(
    ISeatRepository seats,
    ISeatRequestRepository requests,
    IMessagePublisher publisher,
    ICorrelationIdAccessor correlation,
    ILogger<RequestReservationCommandHandler> logger)
    : IRequestHandler<RequestReservationCommand, ReservationAcceptedResult>
{
    public async Task<ReservationAcceptedResult> Handle(RequestReservationCommand command, CancellationToken ct)
    {
        Validate(command);

        var seatNumber = command.SeatNumber.Trim();
        var seat = await seats.FirstOrDefaultAsync(s => s.EventId == command.EventId && s.SeatNumber == seatNumber, ct)
                   ?? throw new SeatNotFoundException(command.EventId, seatNumber);
        if (!seat.IsAvailable) throw new SeatUnavailableException(command.EventId, seatNumber);

        if (command.Price != seat.Price)
        {
            logger.LogWarning(
                "[{CorrelationId}] El precio recibido {RequestedPrice} difiere del catálogo {CatalogPrice} para {SeatNumber}; se usa el del catálogo.",
                correlation.CorrelationId, command.Price, seat.Price, seatNumber);
        }

        var request = new SeatRequest
        {
            ReservationId = Guid.NewGuid(),
            SeatId = seat.SeatId,
            EventId = seat.EventId,
            SeatNumber = seat.SeatNumber,
            UserId = command.UserId,
            Price = seat.Price,
            Status = ReservationStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CorrelationId = correlation.CorrelationId
        };
        seat.IsAvailable = false;

        await requests.AddAsync(request, ct);
        seats.Update(seat);
        await requests.SaveChangesAsync(ct);

        try
        {
            await publisher.PublishAsync(new ReservationRequestedEvent
            {
                ReservationId = request.ReservationId,
                EventId = request.EventId,
                SeatNumber = request.SeatNumber,
                UserId = request.UserId,
                Price = request.Price,
                RequestedAtUtc = request.RequestedAtUtc,
                CorrelationId = request.CorrelationId
            }, QueueNames.BookingRequests, ct);
        }
        catch (MessagePublishException)
        {
            await CompensateAsync(request, seat);
            throw;
        }

        return new ReservationAcceptedResult(request.ReservationId);
    }

    private static void Validate(RequestReservationCommand command)
    {
        if (command.EventId == Guid.Empty) throw new InvalidReservationRequestException("EventId vacío.");
        if (command.UserId == Guid.Empty) throw new InvalidReservationRequestException("UserId vacío.");
        if (string.IsNullOrWhiteSpace(command.SeatNumber)) throw new InvalidReservationRequestException("SeatNumber vacío.");
    }

    /// <summary>
    /// Deshace lo guardado cuando el evento no llegó al broker: elimina el registro Pending y restaura el asiento a
    /// disponible (ambos repositorios comparten el mismo DbContext, un solo SaveChanges los escribe juntos).
    /// Si la compensación también falla, se loguea y se conserva el error original.
    /// </summary>
    private async Task CompensateAsync(SeatRequest request, Seat seat)
    {
        try
        {
            requests.Remove(request);
            seat.IsAvailable = true;
            seats.Update(seat);
            await requests.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[{CorrelationId}] No se pudo compensar la solicitud {ReservationId} tras fallar la publicación.",
                request.CorrelationId, request.ReservationId);
        }
    }
}
