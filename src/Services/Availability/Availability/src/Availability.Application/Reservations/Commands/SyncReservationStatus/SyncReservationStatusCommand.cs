using Availability.Domain.Enums;
using MediatR;

namespace Availability.Application.Reservations.Commands.SyncReservationStatus;

/// <summary>
/// Command (escritura, CQRS): consulta el estado de la reserva en Booking y sincroniza el estado local
/// (<c>SeatRequest.Status</c> y <c>Seat.IsAvailable</c>). Lo despacha GET /api/reservations/{id}.
/// </summary>
public sealed record SyncReservationStatusCommand(Guid ReservationId) : IRequest<ReservationStatusResult>;

/// <param name="ExpiresAtUtc">Solo informado mientras la reserva está retenida.</param>
public sealed record ReservationStatusResult(
    Guid ReservationId,
    ReservationStatus Status,
    string Message,
    string SeatNumber,
    DateTime? ExpiresAtUtc);
