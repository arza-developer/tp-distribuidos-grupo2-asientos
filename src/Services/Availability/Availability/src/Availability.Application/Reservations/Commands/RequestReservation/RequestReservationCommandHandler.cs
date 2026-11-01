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
/// No se rechaza por <c>Seat.IsAvailable</c>: ese flag local puede estar desactualizado y la exclusión mutua
/// la decide Booking (índice único en su base). Availability solo exige que el asiento exista.
/// </para>
/// <para>
/// Orden: guardar (Pending) → publicar. Si la publicación falla se compensa eliminando el registro, de modo que
/// no queda una solicitud Pending huérfana; la excepción técnica sube para responder 503 y nunca 202.
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

        await requests.AddAsync(request, ct);
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
            await CompensateAsync(request);
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

    /// <summary>Quita el registro Pending cuyo evento no llegó al broker. Si también falla, se loguea y se conserva el error original.</summary>
    private async Task CompensateAsync(SeatRequest request)
    {
        try
        {
            requests.Remove(request);
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
