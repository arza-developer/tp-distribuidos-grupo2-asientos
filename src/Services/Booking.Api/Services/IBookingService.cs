using Booking.Api.Models;
using Booking.Api.DTOs;

namespace Booking.Api.Services
{
    public interface IBookingService
    {
        Task<Reservation> TryRetainReservationAsync(ReserveSeatRequest request, CancellationToken cancellationToken = default);
    }
}
