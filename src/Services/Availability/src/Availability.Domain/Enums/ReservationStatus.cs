namespace Availability.Domain.Enums;

/// <summary>Estados de una solicitud de reserva vistos desde Availability.</summary>
public enum ReservationStatus
{
    /// <summary>Recibida y encolada; Booking aún no decidió (consistencia eventual).</summary>
    Pending = 0,
    Retained = 1,
    Confirmed = 2,
    Rejected = 3,
    Expired = 4
}
