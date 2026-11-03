using Availability.Domain.Enums;

namespace Availability.Application.Bookings;

/// <summary>Estado de una reserva según Booking, ya traducido al vocabulario de Availability.</summary>
public sealed record BookingStatusInfo(ReservationStatus Status, DateTime? ExpiresAtUtc, string SeatNumber);
