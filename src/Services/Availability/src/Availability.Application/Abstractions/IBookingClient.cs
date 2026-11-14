using Availability.Application.Bookings;

namespace Availability.Application.Abstractions;

/// <summary>
/// Puerto hacia Booking (solo lectura, paso 7 del TP). La implementación vive en Infrastructure y traduce el
/// vocabulario de Booking (estados en español) al de Availability.
/// </summary>
public interface IBookingClient
{
    /// <summary>
    /// Estado de la reserva en Booking, o <c>null</c> si Booking todavía no la conoce (aún no procesó el mensaje:
    /// consistencia eventual). Lanza <see cref="Exceptions.BookingUnavailableException"/> si no se pudo consultar.
    /// </summary>
    Task<BookingStatusInfo?> GetStatusAsync(Guid reservationId, CancellationToken ct = default);
}
