using Booking.Api.DataAccess.Models;
using Booking.Api.Business.DTOs;
using Booking.Api.DataAccess.Models.Enums;

namespace Booking.Api.Business.Services
{
    public interface IBookingService
    {
        Task<Reservation> TryRetainReservationAsync(ReserveSeatRequest request, CancellationToken cancellationToken = default);
        Task<Reservation> ConfirmReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
        Task<Reservation> RejectReservationAsync(Guid reservationId, ReservationRejectionReason reason, CancellationToken cancellationToken = default);
        Task<int> ExpireOverdueReservationsAsync(CancellationToken cancellationToken = default);
        Task<Reservation> ProcessBookingAsync(ReserveSeatRequest request, decimal amount,CancellationToken cancellationToken = default);
    }
}
