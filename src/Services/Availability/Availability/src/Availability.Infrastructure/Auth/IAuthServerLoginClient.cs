namespace Availability.Infrastructure.Auth;

/// <summary>
/// Login de usuarios contra el Auth Server (grant <c>password</c>) para las páginas MVC.
/// Único punto de acoplamiento con ese flujo, que la rama feature/06-auth-server-m2m marca como "solo para pruebas" (TD-9).
/// </summary>
public interface IAuthServerLoginClient
{
    /// <summary>
    /// Devuelve el access token del usuario, o <c>null</c> si las credenciales son incorrectas.
    /// Si el Auth Server no responde lanza <see cref="Availability.Application.Exceptions.AuthServerUnavailableException"/>.
    /// </summary>
    Task<string?> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default);
}
