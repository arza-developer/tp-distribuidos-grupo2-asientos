using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Availability.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Availability.Web.Mvc.Services;

/// <summary>
/// Inicio y vuelta del login federado (Authorization Code + PKCE contra el Auth Server, que a su vez federa con el IdP externo).
/// Availability NUNCA habla con el IdP ni recibe su <c>id_token</c>: redirige al Auth Server y recibe de vuelta un
/// <c>code</c> de un solo uso que canjea por canal trasero.
/// <para>
/// El estado de la transacción (<c>state</c>, <c>code_verifier</c>, <c>returnUrl</c>) viaja en una cookie propia, HttpOnly,
/// SameSite=Lax, cifrada con Data Protection y con vencimiento: así no hay estado en memoria del servidor (escala a réplicas)
/// y el verifier nunca llega al navegador en claro.
/// </para>
/// </summary>
public sealed class ExternalLoginFlow(IDataProtectionProvider dataProtection, IOptions<AuthServerOptions> options)
{
    public const string CookieName = ".Availability.AuthFlow";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private sealed record FlowState(string State, string Verifier, string? ReturnUrl);

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Availability.ExternalLoginFlow.v1").ToTimeLimitedDataProtector();

    private readonly AuthServerOptions _options = options.Value;

    /// <summary>Genera state + PKCE, los guarda en la cookie y devuelve la URL de <c>/connect/authorize</c> del Auth Server.</summary>
    public string Start(HttpResponse response, string? localReturnUrl)
    {
        var state = PkceGenerator.CreateState();
        var verifier = PkceGenerator.CreateVerifier();

        var payload = JsonSerializer.Serialize(new FlowState(state, verifier, localReturnUrl));
        response.Cookies.Append(CookieName, _protector.Protect(payload, Lifetime), CookieOptions());

        return QueryHelpers.AddQueryString($"{_options.PublicUrl.TrimEnd('/')}/connect/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.FrontendClientId,
            ["redirect_uri"] = _options.FrontendRedirectUri,
            ["code_challenge"] = PkceGenerator.CreateChallenge(verifier),
            ["code_challenge_method"] = PkceGenerator.ChallengeMethod,
            ["state"] = state,
            ["idp_hint"] = "external" // el Auth Server salta su formulario y va directo al IdP externo
        });
    }

    /// <summary>
    /// Toma (y SIEMPRE borra) la cookie de flujo y comprueba el <c>state</c> en tiempo constante. Devuelve el verifier y el
    /// <c>returnUrl</c> si la vuelta corresponde a un inicio hecho por este navegador y vigente; si no, <c>null</c>.
    /// </summary>
    public (string Verifier, string? ReturnUrl)? Consume(HttpRequest request, HttpResponse response, string? returnedState)
    {
        var raw = request.Cookies[CookieName];
        response.Cookies.Delete(CookieName, CookieOptions()); // un flujo sirve una sola vez, salga bien o mal
        if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(returnedState)) return null;

        FlowState? flow;
        try
        {
            flow = JsonSerializer.Deserialize<FlowState>(_protector.Unprotect(raw));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null; // cookie manipulada, vencida o de otra clave
        }

        if (flow is null) return null;
        var expected = Encoding.UTF8.GetBytes(flow.State);
        var actual = Encoding.UTF8.GetBytes(returnedState);
        return CryptographicOperations.FixedTimeEquals(expected, actual) ? (flow.Verifier, flow.ReturnUrl) : null;
    }

    private static CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax, // la vuelta es una navegación de primer nivel (GET) desde el Auth Server
        Secure = false,              // SameAsRequest: el laboratorio corre en HTTP (limitación M7)
        IsEssential = true,
        MaxAge = Lifetime,
        Path = "/Account"
    };
}
