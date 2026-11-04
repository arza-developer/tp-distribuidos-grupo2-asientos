namespace Availability.Domain.Exceptions;

/// <summary>La solicitud de reserva no cumple los datos mínimos (identificadores vacíos, asiento sin número).</summary>
public sealed class InvalidReservationRequestException(string reason)
    : BusinessValidationException("reservation.invalid_request", $"Solicitud de reserva inválida: {reason}");
