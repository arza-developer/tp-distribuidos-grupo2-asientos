using Availability.Infrastructure.Auth;

namespace Availability.Tests.Support;

/// <summary>
/// Auth Server falso para el login de las páginas. Usuarios conocidos: <c>cliente1/cliente1-dev</c> (rol Client, como en
/// la rama 06), <c>servicio/pw</c> (token de rol Service) y <c>&lt;b&gt;x&lt;/b&gt;/pw</c> (para probar la codificación HTML).
/// </summary>
public sealed class FakeAuthServerLoginClient : IAuthServerLoginClient
{
    public const string HtmlUser = "<b>x</b>";

    /// <summary>Segundo usuario (rol Client) para probar que no ve reservas ajenas; su UserId es otro Guid.</summary>
    public static readonly Guid OtherUserId = Guid.Parse("9dd07a86-7777-8888-9999-4e185a88c1bb");

    public Dictionary<(string User, string Password), string> Tokens { get; } = new()
    {
        [("cliente1", "cliente1-dev")] = TestJwt.ForClient(),
        [("servicio", "pw")] = TestJwt.ForService(),
        [(HtmlUser, "pw")] = TestJwt.ForClient(),
        [("otro", "pw")] = TestJwt.ForClient(OtherUserId)
    };

    public Exception? Throw { get; set; }

    public List<(string User, string Password, string CorrelationId)> Calls { get; } = [];

    public Task<string?> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default)
    {
        lock (Calls) Calls.Add((username, password, correlationId));
        if (Throw is not null) throw Throw;
        return Task.FromResult(Tokens.TryGetValue((username, password), out var token) ? token : null);
    }

    /// <summary>Códigos de autorización conocidos (code → access token); se consumen al canjearlos, como en el Auth Server real.</summary>
    public Dictionary<string, string> Codes { get; } = new();

    public List<(string Code, string Verifier, string RedirectUri, string ClientId)> Exchanges { get; } = [];

    public Task<string?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId,
        string correlationId, CancellationToken ct = default)
    {
        lock (Exchanges) Exchanges.Add((code, codeVerifier, redirectUri, clientId));
        if (Throw is not null) throw Throw;
        lock (Codes)
        {
            if (!Codes.Remove(code, out var token)) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(token);
        }
    }

    public void Reset()
    {
        Throw = null;
        lock (Calls) Calls.Clear();
        lock (Exchanges) Exchanges.Clear();
        lock (Codes) Codes.Clear();
    }
}
