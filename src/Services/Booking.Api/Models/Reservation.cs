using Booking.Api.Models.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Booking.Api.Models;

public class Reservation
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid EventId { get; set; }

    public string SeatNumber { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ReservationStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public ReservationRejectionReason? RejectionReason { get; set; }
}
