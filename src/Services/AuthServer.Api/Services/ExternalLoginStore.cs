using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthServer.Api.Services;

// Lo que la boletería anota cuando manda a alguien al proveedor externo:
// el pedido ORIGINAL del front, para retomarlo cuando la persona vuelva.
public record PendingExternalLogin(
    string ClientId,       // la app que pidió el login (el front)
    string RedirectUri,    // a dónde hay que devolverle el código
    string CodeChallenge,  // la huella del talón PKCE del front
    string FrontState,     // el state del front, que le devolvemos igual
    string Nonce,          // el valor que tiene que volver adentro del id_token
    DateTime ExpiresAtUtc);

// [3rd Party IdP]: logins externos en curso. La clave es el "state" que viaja al proveedor y vuelve.
// [Decisión TP]: en memoria, como los códigos de autorización.
public sealed class ExternalLoginStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, PendingExternalLogin> _pending = new();

    // Anota el pedido del front y devuelve el state y el nonce para mandar al proveedor.
    public (string State, string Nonce) Start(string clientId, string redirectUri, string codeChallenge, string frontState)
    {
        var state = NewRandom();
        var nonce = NewRandom();
        _pending[state] = new PendingExternalLogin(clientId, redirectUri, codeChallenge, frontState, nonce,
            DateTime.UtcNow.Add(Lifetime));
        return (state, nonce);
    }

    // Devuelve el login pendiente y lo BORRA: cada state sirve una sola vez.
    public PendingExternalLogin? Take(string state)
    {
        if (string.IsNullOrEmpty(state)) return null;
        if (!_pending.TryRemove(state, out var entry)) return null;
        return entry.ExpiresAtUtc < DateTime.UtcNow ? null : entry;
    }

    // Valor al azar con un generador criptográfico: imposible de adivinar.
    private static string NewRandom() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}