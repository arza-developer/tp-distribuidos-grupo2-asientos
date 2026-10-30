using Booking.Api.Data;
using Booking.Api.Models.Enums;
using Booking.Api.Models;
using Microsoft.Data.Sqlite;
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
            _context.Reservations.Add(reservation);// Agrega la reserva al contexto de la base de datos

            try
            {
                await _context.SaveChangesAsync(cancellationToken);// Intenta guardar los cambios en la base de datos
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteExtendedErrorCode == 2067)// El catch se ejecuta si hay un conflicto de clave única (constraint violation) en la base de datos SQLite
            {
              _context.Entry(reservation).State = EntityState.Detached;// Desvincula la entidad del contexto para evitar problemas de seguimiento de cambios

                throw new ReservationUniqueConflictException(ex);// Lanza una excepción personalizada para indicar que hubo un conflicto de reserva única a BookingService
            }

        }

        // Expira una reserva si ha pasado su tiempo de expiración
        public async Task<int> ExpireIfDueAsync(Guid reservationId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
          return await _context.Reservations.Where(r => 
              r.Id == reservationId &&
              r.Status == ReservationStatus.Retained && 
              r.ExpiresAtUtc != null &&
              r.ExpiresAtUtc <= nowUtc)
              .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Status, ReservationStatus.Expired), cancellationToken);
        }

        // Recupera una reserva por su ID
        public async Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        }

        // Confirma una reserva si es válida (es decir, si está retenida y no ha expirado)
        public async Task<int> ConfirmIfValidAsync(Guid reservationId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations.Where(r =>
                r.Id == reservationId &&
                r.Status == ReservationStatus.Retained &&
                r.ExpiresAtUtc != null &&
                r.ExpiresAtUtc > nowUtc)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Status, ReservationStatus.Confirmed), cancellationToken);
        }

        // Rechaza una reserva si está en estado "Retained"
        public async Task<int> RejectIfRetainedAsync(Guid reservationId, ReservationRejectionReason reason, CancellationToken cancellationToken = default)
        {
            // Actualiza el estado de la reserva a "Rejected" si está en estado "Retained"
            return await _context.Reservations.Where(r =>
                r.Id == reservationId &&
                r.Status == ReservationStatus.Retained)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, ReservationStatus.Rejected)
                    .SetProperty(r => r.RejectionReason,(ReservationRejectionReason?)reason)
                    .SetProperty(r => r.ExpiresAtUtc, (DateTime?)null), cancellationToken);
        }
    }
}