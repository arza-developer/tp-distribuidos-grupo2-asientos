namespace Availability.Domain.Exceptions;

/// <summary>El asiento existe pero ya no está disponible (ya fue reservado): conflicto de estado, no "inexistente".</summary>
public sealed class SeatUnavailableException(Guid eventId, string seatNumber)
    : BusinessConflictException("seat.unavailable", $"El asiento '{seatNumber}' del evento '{eventId}' no está disponible.");
