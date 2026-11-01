using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthServer.Api.Services;

// Lo que la boletería anota de cada comprobante de retiro (código de autorización).
public record AuthorizationCode(
    string ClientId,       // para qué app es
    string RedirectUri,    // a qué dirección se devolvió (al canjear tiene que ser la misma)
    string CodeChallenge,  // la huella del talón (PKCE)
    string UserId,         // para quién es la entrada
    string Role,
    DateTime ExpiresAtUtc);

// [Authorization Code]: registro en memoria de los códigos pendientes de canje.
// [Decisión TP]: en memoria. Se pierden si el AuthServer se reinicia, y no se comparten entre réplicas.
// En producción iría en un almacenamiento compartido (Redis, base de datos).
public sealed class AuthorizationCodeStore
{
    // Vida corta: solo tiene que alcanzar para que el navegador vuelva a la app y la app lo canjee.
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    // ConcurrentDictionary: seguro aunque lleguen muchos pedidos a la vez (varios hilos).
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

    public string Create(string clientId, string redirectUri, string codeChallenge, string userId, string role)
    {
        // 32 bytes al azar con un generador criptográfico: imposible de adivinar.
        var code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        _codes[code] = new AuthorizationCode(clientId, redirectUri, codeChallenge, userId, role,
            DateTime.UtcNow.Add(Lifetime));

        return code;
    }

    // Devuelve el código y lo BORRA del registro: un comprobante sirve UNA sola vez.
    // Si no existe o venció, devuelve null.
    public AuthorizationCode? Redeem(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        if (!_codes.TryRemove(code, out var entry)) return null;
        if (entry.ExpiresAtUtc < DateTime.UtcNow) return null;
        return entry;
    }

    // [PKCE - Clase 12]: ¿encaja el talón?
    // Calcula la huella (SHA256) del code_verifier que trae la app y la compara con el code_challenge guardado.
    // La huella no se puede "dar vuelta": viendo el challenge no se puede averiguar el verifier.
    public static bool VerifyPkce(string codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier)) return false;

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(hash) == codeChallenge;
    }
}