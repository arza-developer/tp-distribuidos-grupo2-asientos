namespace Availability.Application.Seats;

/// <summary>Vista pública de un asiento. No expone el Id interno de la BBDD.</summary>
public sealed record SeatDto(string SeatNumber, decimal Price, bool IsAvailable);
