namespace Availability.Infrastructure.Auth;

/// <summary>Token de acceso M2M (Client Credentials) de Availability, con cache hasta poco antes de su vencimiento.</summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Devuelve un token vigente (del cache o pidiendo uno nuevo al Auth Server). Si no se puede obtener lanza
    /// <see cref="Availability.Application.Exceptions.BookingUnavailableException"/> (el llamado a Booking no puede salir).
    /// </summary>
    Task<string> GetTokenAsync(string correlationId, CancellationToken ct = default);

    /// <summary>Descarta el token si es el que está en cache (p. ej. Booking lo rechazó con 401).</summary>
    void Invalidate(string token);
}
