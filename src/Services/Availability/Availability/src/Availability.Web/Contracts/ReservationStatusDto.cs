namespace Availability.Web.Contracts;

/// <summary>
/// Respuesta de GET /api/reservations/{id}. Aditiva respecto del stub histórico ({reservationId, status, message}):
/// suma <c>seatNumber</c> y <c>expiresAtUtc</c> (este último solo mientras la reserva está retenida).
/// </summary>
public sealed record ReservationStatusDto(
    Guid ReservationId,
    string Status,
    string Message,
    string SeatNumber,
    DateTime? ExpiresAtUtc);
