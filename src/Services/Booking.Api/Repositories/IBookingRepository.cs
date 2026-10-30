using Booking.Api.Models;

namespace Booking.Api.Repositories
{
    public interface IBookingRepository
    {
        Task<Reservation?> GetByRequestIdAsync(Guid requestId);
        Task<bool> HasActiveReservationAsync(Guid eventId, string seatNumber);
        Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken = default);
        Task<int> ExpireOverdueReservationsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);   
    }
}
