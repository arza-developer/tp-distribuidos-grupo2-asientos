namespace Booking.Api.DTOs
{
    public class ReserveSeatRequest
    {
        public Guid RequestId { get; set; }

        public Guid EventId { get; set; }

        public string SeatNumber { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }
}
