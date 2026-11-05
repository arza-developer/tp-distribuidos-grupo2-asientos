using Booking.Api.Business.DTOs;
using Booking.Api.DataAccess.Models;
using Booking.Api.DataAccess.Models.Enums;
using Booking.Api.Business.Interfaces;
using Booking.Api.Core.Exceptions;

namespace Booking.Api.Business.Services
{
    public class BookingService: IBookingService
    {
        private readonly IBookingRepository _repository;
        private readonly IPaymentClient _paymentClient;

        public BookingService(IBookingRepository repository, IPaymentClient paymentClient)
        {
            _repository = repository;
            _paymentClient = paymentClient;
        }

        // Intentar retener una reserva
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

        // Confirmar una reserva
        public async Task<Reservation> ConfirmReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            // Validar el id de la reserva
            if (reservationId == Guid.Empty)
            {
                throw new ArgumentException("El id de la reserva es invalido.");
            }

            // Intentar confirmar la reserva en la base de datos
            int updateRows = await _repository.ConfirmIfValidAsync(reservationId, DateTime.UtcNow, cancellationToken);

            // Recuperamos la reserva para verificar su estado
            var reservation = await _repository.GetByIdAsync(reservationId);

            if(reservation == null)
            {
                throw new KeyNotFoundException("La reserva no existe.");
            }

            // Si la reserva fue exitosa o ya estaba confirmada, devolvemos la reserva confirmada
            if (reservation.Status == ReservationStatus.Confirmed)
            {
                return reservation;
            }

            throw new InvalidOperationException("No se puede confirmar una reserva en estado {reservation.Status}.");
        }

        // Rechazar una reserva
        public async Task<Reservation> RejectReservationAsync(Guid reservationId, ReservationRejectionReason reason, CancellationToken cancellationToken = default)
        {
            // Validar el id de la reserva
            if (reservationId == Guid.Empty)
            {
                throw new ArgumentException("El id de la reserva es invalido.");
            }

            // Validar el motivo de rechazo
            if (!Enum.IsDefined(reason))
            {
                throw new ArgumentException("El motivo de rechazo es invalido.");
            }

            // Intentar rechazar la reserva en la base de datos
            await _repository.RejectIfRetainedAsync(reservationId, reason, cancellationToken);

            // Recuperamos la reserva para verificar su estado
            var reservation = await _repository.GetByIdAsync(reservationId);

            if(reservation == null)
            {
                throw new KeyNotFoundException("La reserva no existe.");
            }

            // Si fue rechazada por el mismo motivo, devolvemos la reserva rechazada
            if (reservation.Status == ReservationStatus.Rejected && reservation.RejectionReason == reason)
            {
                return reservation;
            }

            throw new InvalidOperationException($"No se puede rechazar la reserva. " + $"Estado actual: {reservation.Status}. " + $"Motivo actual: {reservation.RejectionReason}.");
        }

        // Vencer en lote todas las reservas retenidas cuyo plazo ya paso
        public Task<int> ExpireOverdueReservationsAsync(CancellationToken cancellationToken  = default)
        {
            return _repository.ExpireOverdueReservationsAsync(DateTime.UtcNow, cancellationToken);
        }

        public async Task<Reservation> ProcessBookingAsync(ReserveSeatRequest request, decimal amount, CancellationToken cancellationToken = default)
        {
            //Validamos el importe antes de procesar la reserva
            if (amount <= 0)
            {
                throw new ArgumentException("El importe debe ser mayor a cero.", nameof(amount));
            }

            // Intentar retener la reserva
            var reservation = await TryRetainReservationAsync(request, cancellationToken);
            
            // Si la reserva ya tiene resultado final, devolvemos la reserva sin volver a cobrar 
            if (reservation.Status != ReservationStatus.Retained)
            {
                return reservation;
            }
            
            //No iniciamos un cobro si la reserva ya vencio
            if (reservation.ExpiresAtUtc == null || reservation.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("La reserva ya venció.");
            }

            //Solicitamos el pago mediante el cliente gRPC, es un llamada sincronica, booking service espera la respuesta de payment service para continuar
            var paymentResult = await _paymentClient.ProcessPaymentAsync(reservation.Id, reservation.UserId, amount, reservation.RequestId.ToString(), cancellationToken);

            //Interpretamos el resultado del pago y actualizamos la reserva en consecuencia
            switch (paymentResult.Outcome)
            {
                case PaymentOutcome.Confirmed:
                    //Intentamos confirmar la reserva
                    return await ConfirmReservationAsync(reservation.Id, cancellationToken);
                
                case PaymentOutcome.Failed:
                    //Intentamos rechazar la reserva por pago fallido
                    return await RejectReservationAsync(reservation.Id, ReservationRejectionReason.PaymentFailed, cancellationToken);
                
                case PaymentOutcome.Unavailable:
                    //No sabemos si el cobro llego a procesarse, conservamos Retained para permitir recuperación
                    throw new PaymentUnavailableException("No se pudo determinar el resultado del pago.");

                default:
                    throw new InvalidOperationException($"Resultado de pago desconocido: {paymentResult.Outcome}");
            }
        }

        public async Task<Reservation?> GetBookingStatusAsync(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("El identificador de la solicitud es invalido.");
            } 
            
            return await _repository.GetByRequestIdAsync(requestId);
        }
    }
}
