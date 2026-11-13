namespace Availability.Web.Contracts;

/// <summary>Cuerpo de POST /api/reservations (contrato heredado de Availability.Api).</summary>
public sealed record ReservationRequestDto(Guid EventId, string SeatNumber, Guid UserId, decimal Price);
