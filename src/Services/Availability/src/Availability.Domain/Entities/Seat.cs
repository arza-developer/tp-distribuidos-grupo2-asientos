namespace Availability.Domain.Entities;

/// <summary>
/// Asiento de un evento. Entidad ANÉMICA (requisito 3.a.ii del TP): solo datos, sin comportamiento.
/// Las reglas y la orquestación viven en los casos de uso de la capa Application.
/// </summary>
public class Seat : EntityBase
{
    /// <summary>Referencia externa e inmutable del asiento (la que conocen Broker y Booking).</summary>
    public Guid SeatId { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Identificador visible, p. ej. "A-1".</summary>
    public string SeatNumber { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; }
}
