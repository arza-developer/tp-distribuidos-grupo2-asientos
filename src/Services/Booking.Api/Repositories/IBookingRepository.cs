using Booking.Api.Models;
using Booking.Api.Models.Enums;

namespace Booking.Api.Repositories
{
    public interface IBookingRepository
    {
        Task<Reservation?> GetByRequestIdAsync(Guid requestId);
        Task<bool> HasActiveReservationAsync(Guid eventId, string seatNumber);
        Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken = default);
        Task<int> ExpireIfDueAsync(Guid reservationId, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
        Task<int> ConfirmIfValidAsync(Guid reservationId, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<int> RejectIfRetainedAsync(Guid reservationId, ReservationRejectionReason reason, CancellationToken cancellationToken = default);
    }
}
