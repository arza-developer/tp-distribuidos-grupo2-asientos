namespace Availability.Application.Exceptions;

/// <summary>
/// Fallo TÉCNICO al hablar con el Auth Server en el login (caído, timeout, 5xx o respuesta no interpretable).
/// NO es una <c>BusinessException</c>: credenciales incorrectas no lanzan esto (el cliente de login devuelve <c>null</c>).
/// </summary>
public sealed class AuthServerUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
