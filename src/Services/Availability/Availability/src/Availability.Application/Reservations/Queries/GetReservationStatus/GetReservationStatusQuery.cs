using Availability.Domain.Enums;
using MediatR;

namespace Availability.Application.Reservations.Queries.GetReservationStatus;

/// <summary>Query (lectura, CQRS): estado actual de una reserva originada en Availability.</summary>
public sealed record GetReservationStatusQuery(Guid ReservationId) : IRequest<ReservationStatusResult>;

/// <param name="ExpiresAtUtc">Solo informado mientras la reserva está retenida.</param>
public sealed record ReservationStatusResult(
    Guid ReservationId,
    ReservationStatus Status,
    string Message,
    string SeatNumber,
    DateTime? ExpiresAtUtc);
