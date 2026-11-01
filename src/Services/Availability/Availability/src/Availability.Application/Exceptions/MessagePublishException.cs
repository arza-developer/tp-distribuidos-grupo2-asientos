namespace Availability.Application.Exceptions;

/// <summary>
/// Fallo TÉCNICO al publicar en el broker (cola inalcanzable, timeout, etc.).
/// NO es una <c>BusinessException</c>: no es culpa del cliente, Web lo traduce a 503.
/// La implementación de mensajería (Infrastructure) envuelve aquí los errores del SDK para que
/// Application no conozca tipos de Azure.
/// </summary>
public sealed class MessagePublishException(string message, Exception? innerException = null)
    : Exception(message, innerException);
