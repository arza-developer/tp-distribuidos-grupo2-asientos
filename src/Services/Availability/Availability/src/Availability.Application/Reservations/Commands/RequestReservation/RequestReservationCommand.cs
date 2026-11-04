using MediatR;

namespace Availability.Application.Reservations.Commands.RequestReservation;

/// <summary>Command (escritura, CQRS): solicitud de reserva de un asiento.</summary>
/// <param name="Price">Se acepta por compatibilidad con el contrato HTTP; se publica el precio del catálogo.</param>
public sealed record RequestReservationCommand(Guid EventId, string SeatNumber, Guid UserId, decimal Price)
    : IRequest<ReservationAcceptedResult>;

/// <summary>Resultado: solo la referencia externa de la reserva (nunca el Id interno).</summary>
public sealed record ReservationAcceptedResult(Guid ReservationId);
