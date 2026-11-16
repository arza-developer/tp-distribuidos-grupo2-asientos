using System.Text.Json.Serialization;

namespace Availability.Web.Contracts;

/// <summary>
/// Respuesta de GET /api/reservations/{id}. Aditiva respecto del stub histórico ({reservationId, status, message}):
/// suma <c>seatNumber</c>, <c>expiresAtUtc</c> (solo mientras la reserva está retenida) y <c>rejectionReason</c>
/// (solo si Booking rechazó la reserva e informó el motivo; se omite en los demás casos).
/// </summary>
public sealed record ReservationStatusDto(
    Guid ReservationId,
    string Status,
    string Message,
    string SeatNumber,
    DateTime? ExpiresAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RejectionReason = null);
