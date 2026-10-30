using Booking.Api.DTOs;
using Booking.Api.Models;
using Booking.Api.Models.Enums;
using Booking.Api.Repositories;

namespace Booking.Api.Services
{
    public class BookingService: IBookingService
    {
        private readonly IBookingRepository _repository;

        public BookingService(IBookingRepository repository)
        {
            _repository = repository;
        }

        public async Task<Reservation> TryRetainReservationAsync( ReserveSeatRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if(request.RequestId == Guid.Empty ||
               request.EventId == Guid.Empty ||
               string.IsNullOrWhiteSpace(request.SeatNumber)||
               string.IsNullOrWhiteSpace(request.UserId))
            {
                throw new ArgumentException("La solicitus contiene datos invalidos.");
            }

            string seatNumber = request.SeatNumber.Trim().ToUpperInvariant();

            //Comprobar si la solicitud ya fue procesada 
            var existing = await _repository.GetByRequestIdAsync(request.RequestId);

            if(existing != null)
            {
                return existing;
            }

            //Consultar si el asiento esta ocupado
            bool occupied = await _repository.HasActiveReservationAsync(request.EventId, seatNumber);

            //Crear la reserva con el estado correspondiente
            DateTime now = DateTime.UtcNow;

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                RequestId = request.RequestId,
                EventId = request.EventId,
                SeatNumber = request.SeatNumber,
                UserId = request.UserId,
                CreatedAtUtc = now,
                Status = occupied ? ReservationStatus.Rejected : ReservationStatus.Retained,
                ExpiresAtUtc = occupied ? null : now.AddMinutes(10),
                RejectionReason = occupied ? ReservationRejectionReason.SeatOccupied : null,
            };

            //Guardar la reserva
            try
            {
                // Intentar guardar la reserva en la base de datos
                await _repository.AddReservationAsync(reservation, cancellationToken);
                return reservation;
            }
            catch (ReservationUniqueConflictException)
            {
                // Verifica si la request ya fue procesada por otro hilo
                var existingByRequestId = await _repository.GetByRequestIdAsync(request.RequestId);

                if (existingByRequestId != null)
                {
                    return existingByRequestId;
                }

                // Verifica si el asiento ya estaba ocupado
                bool seatOccupiedNow = await _repository.HasActiveReservationAsync(request.EventId, seatNumber);
                
                if (!seatOccupiedNow)
                {
                    throw;
                }

                // Si el asiento estaba ocupado, crea una reserva rechazada
                reservation.Status = ReservationStatus.Rejected;
                reservation.ExpiresAtUtc = null;
                reservation.RejectionReason = ReservationRejectionReason.SeatOccupied;

                try
                {
                    // Intenta guardar la reserva rechazada en la base de datos
                    await _repository.AddReservationAsync(reservation, cancellationToken);
                    return reservation;
                }
                catch (ReservationUniqueConflictException)
                {
                    // Si ocurre una excepción de conflicto único, verifica si otro hilo ya procesó la solicitud y devuelve la reserva existente
                    var duplicate = await _repository.GetByRequestIdAsync(request.RequestId);

                    if (duplicate != null)
                    {
                        return duplicate;
                    }

                    throw;
                }
            }
        }
    }
}
