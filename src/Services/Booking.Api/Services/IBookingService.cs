using Booking.Api.Models;
using Booking.Api.DTOs;
using Booking.Api.Models.Enums;

namespace Booking.Api.Services
{
    public interface IBookingService
    {
        Task<Reservation> TryRetainReservationAsync(ReserveSeatRequest request, CancellationToken cancellationToken = default);
        Task<Reservation> ConfirmReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
        Task<Reservation> RejectReservationAsync(Guid reservationId, ReservationRejectionReason reason, CancellationToken cancellationToken = default);
        Task<Reservation> TryExpireReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    }
}
