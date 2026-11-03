using Availability.Domain.Enums;

namespace Availability.Domain.Entities;

/// <summary>
/// Solicitud de reserva recibida por Availability (entidad ANÉMICA, sin comportamiento).
/// <see cref="EntityBase.Id"/> es interno; <see cref="ReservationId"/> es la referencia externa
/// (la que ven el cliente, el broker y Booking). El asiento se referencia por su Guid
/// <see cref="SeatId"/>, nunca por el Id interno.
/// </summary>
public class SeatRequest : EntityBase
{
    public Guid ReservationId { get; set; }

    /// <summary>Referencia externa del asiento (<c>Seat.SeatId</c>).</summary>
    public Guid SeatId { get; set; }

    public Guid EventId { get; set; }

    public string SeatNumber { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public decimal Price { get; set; }

    public ReservationStatus Status { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
