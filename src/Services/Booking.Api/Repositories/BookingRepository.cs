using Booking.Api.Data;
using Booking.Api.Models.Enums;
using Booking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Repositories
{
    public class BookingRepository: IBookingRepository
    {
        private readonly BookingDbContext _context;
        public BookingRepository(BookingDbContext context)
        {
            _context = context;
        }

        // Busca si ya se proceso una reserva para un requestId específico
        public async Task<Reservation?> GetByRequestIdAsync (Guid requestId)
        {
            return await _context.Reservations
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.RequestId == requestId);
        }
        
        //Busca reservas activas para un evento y asiento específico
        public async Task<bool> HasActiveReservationAsync(Guid eventId, string seatNumber)
        {
            return await _context.Reservations.AnyAsync(r => 
            r.EventId == eventId && 
            r.SeatNumber == seatNumber && 
            (r.Status == ReservationStatus.Retained || 
            r.Status == ReservationStatus.Confirmed));
        }

        // Agrega una nueva reserva a la base de datos
        public async Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken = default)
        {
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Vence las reservas retenidas cuyo tiempo de retencion ya paso.
        // [Decision: UPDATE atomico con ExecuteUpdateAsync] Un solo UPDATE en la base, sin cargar las reservas
        // en memoria. El filtro Status = Retained evita pisar una reserva confirmada justo antes del ciclo.
        // El asiento se libera solo: el indice unico filtrado solo cuenta reservas Retained o Confirmed.
        public async Task<int> ExpireOverdueReservationsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .Where(r => r.Status == ReservationStatus.Retained && r.ExpiresAtUtc <= nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, ReservationStatus.Expired),
                    cancellationToken);
        }
    }
}