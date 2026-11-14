namespace Availability.Application.Exceptions;

/// <summary>
/// Fallo TÉCNICO al consultar a Booking (caído, timeout, 5xx o respuesta no interpretable).
/// NO es una <c>BusinessException</c>: Web lo traduce a 503 y el estado de la reserva queda "desconocido".
/// </summary>
public sealed class BookingUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
