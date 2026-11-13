namespace Availability.Infrastructure.Auth;

/// <summary>
/// Login de usuarios contra el Auth Server para las páginas MVC. Dos flujos: grant <c>password</c> (marcado "solo para
/// pruebas" en el Auth Server, TD-9) y canje del código de Authorization Code + PKCE (login federado por el IdP externo,
/// Fase 8). Único punto de acoplamiento de Availability con esos flujos.
/// </summary>
public interface IAuthServerLoginClient
{
    /// <summary>
    /// Devuelve el access token del usuario, o <c>null</c> si las credenciales son incorrectas.
    /// Si el Auth Server no responde lanza <see cref="Availability.Application.Exceptions.AuthServerUnavailableException"/>.
    /// </summary>
    Task<string?> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default);

    /// <summary>
    /// Canjea el <paramref name="code"/> de un solo uso (grant <c>authorization_code</c>, canal trasero) con el
    /// <paramref name="codeVerifier"/> de PKCE. Devuelve el access token, o <c>null</c> si el Auth Server rechaza el código
    /// (<c>invalid_grant</c>: inválido, vencido, ya usado, o <c>redirect_uri</c>/PKCE que no coinciden).
    /// Si el Auth Server no responde lanza <see cref="Availability.Application.Exceptions.AuthServerUnavailableException"/>.
    /// </summary>
    Task<string?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId,
        string correlationId, CancellationToken ct = default);
}
