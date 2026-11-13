using Availability.Domain.Enums;
using MediatR;

namespace Availability.Application.Reservations.Commands.SyncReservationStatus;

/// <summary>
/// Command (escritura, CQRS): consulta el estado de la reserva en Booking y sincroniza el estado local
/// (<c>SeatRequest.Status</c> y <c>Seat.IsAvailable</c>). Lo despacha GET /api/reservations/{id}.
/// </summary>
public sealed record SyncReservationStatusCommand(Guid ReservationId) : IRequest<ReservationStatusResult>;

/// <param name="ExpiresAtUtc">Solo informado mientras la reserva está retenida.</param>
/// <param name="Message">Texto del estado para quien consulta (lo decide este handler).</param>
/// <param name="RejectionReason">Motivo que informa Booking cuando rechazó la reserva; <c>null</c> en los demás estados.</param>
public sealed record ReservationStatusResult(
    Guid ReservationId,
    string SeatNumber,
    ReservationStatus Status,
    DateTime? ExpiresAtUtc,
    string Message,
    string? RejectionReason = null);
