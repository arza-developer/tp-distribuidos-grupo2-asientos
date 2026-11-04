namespace Availability.Domain.Exceptions;

/// <summary>El asiento solicitado no existe para el evento indicado (paso 3 del TP: "verifica que el asiento exista").</summary>
public sealed class SeatNotFoundException(Guid eventId, string seatNumber)
    : BusinessNotFoundException("seat.not_found", $"El asiento '{seatNumber}' no existe para el evento '{eventId}'.");
