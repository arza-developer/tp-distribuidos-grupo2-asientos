namespace Availability.Domain.Exceptions;

/// <summary>No existe una solicitud de reserva con ese identificador en Availability.</summary>
public sealed class ReservationNotFoundException(Guid reservationId)
    : BusinessNotFoundException("reservation.not_found", $"No existe la reserva '{reservationId}'.");
