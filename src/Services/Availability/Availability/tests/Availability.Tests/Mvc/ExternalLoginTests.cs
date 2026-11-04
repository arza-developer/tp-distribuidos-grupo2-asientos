using System.Net;
using System.Web;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Availability.Tests.Api;
using Availability.Tests.Support;
using Availability.Web.Mvc.Services;

namespace Availability.Tests.Mvc;

/// <summary>
/// Login federado (Fase 8): Availability redirige al Auth Server (Authorization Code + PKCE, idp_hint=external) y canjea el
/// código que vuelve en /Account/Callback. Nunca interviene el IdP ni su id_token. El Auth Server se reemplaza por
/// <see cref="FakeAuthServerLoginClient"/> (el canje se prueba aparte en AuthServerLoginClientTests).
/// </summary>
public class ExternalLoginTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public ExternalLoginTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.LoginClient.Reset();
        _factory.Publisher.Reset();
    }

    /// <summary>Hace el POST de inicio (como el botón de la página) y devuelve la respuesta.</summary>
    private static async Task<HttpResponseMessage> StartAsync(HttpClient client, string? returnUrl = null)
    {
        var token = await client.GetAntiforgeryTokenAsync("/Account/Login");
        var fields = new Dictionary<string, string>();
        if (returnUrl is not null) fields["returnUrl"] = returnUrl;
        return await client.PostFormAsync("/Account/LoginExternal", fields, token);
    }

    private static System.Collections.Specialized.NameValueCollection QueryOf(HttpResponseMessage response) =>
        HttpUtility.ParseQueryString(response.Headers.Location!.Query);

    // ---------- inicio ----------

    [Fact]
    public async Task LoginPage_OffersTheExternalButton_InAFormWithAntiforgery()
    {
        var html = await _factory.CreateBrowserClient().GetStringAsync("/Account/Login");

        Assert.Contains("Ingresar con proveedor externo", html);
        Assert.Contains("action=\"/Account/LoginExternal\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task Start_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await _factory.CreateBrowserClient().PostFormAsync("/Account/LoginExternal", [], antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Start_RedirectsToTheAuthServerAuthorizeEndpoint_WithPkceStateAndExternalHint()
    {
        var response = await StartAsync(_factory.CreateBrowserClient());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("http://localhost:5104/connect/authorize", location.GetLeftPart(UriPartial.Path)); // PublicUrl, no la BaseUrl interna
        var q = QueryOf(response);
        Assert.Equal("code", q["response_type"]);
        Assert.Equal("frontend", q["client_id"]);
        Assert.Equal("http://localhost:5101/Account/Callback", q["redirect_uri"]);
        Assert.Equal("S256", q["code_challenge_method"]);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", q["code_challenge"]!);
        Assert.False(string.IsNullOrEmpty(q["state"]));
        Assert.Equal("external", q["idp_hint"]);
        Assert.Empty(_factory.LoginClient.Calls); // iniciar no contacta al Auth Server por canal trasero
    }

    [Fact]
    public async Task Start_SetsAnEncryptedShortLivedHttpOnlyFlowCookie_ThatDoesNotLeakTheVerifier()
    {
        var response = await StartAsync(_factory.CreateBrowserClient());

        var cookie = response.SetCookies().Single(c => c.StartsWith(ExternalLoginFlow.CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=300", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/Account", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(QueryOf(response)["state"]!, cookie); // el contenido está cifrado
    }


    // ---------- vuelta ----------

    private async Task<(HttpClient Client, string State, string Challenge)> StartedAsync(string? returnUrl = null)
    {
        var client = _factory.CreateBrowserClient();
        var q = QueryOf(await StartAsync(client, returnUrl));
        return (client, q["state"]!, q["code_challenge"]!);
    }

    [Fact]
    public async Task Callback_Success_ExchangesTheCodeWithTheMatchingVerifier_AndOpensTheSession()
    {
        var (client, state, challenge) = await StartedAsync();
        _factory.LoginClient.Codes["cod-ok"] = TestJwt.ForClient();

        var response = await client.GetAsync($"/Account/Callback?code=cod-ok&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.RedirectLocation());
        Assert.NotNull(response.SessionSetCookie());

        var exchange = Assert.Single(_factory.LoginClient.Exchanges);
        Assert.Equal("cod-ok", exchange.Code);
        Assert.Equal("frontend", exchange.ClientId);
        Assert.Equal("http://localhost:5101/Account/Callback", exchange.RedirectUri);
        Assert.Equal(challenge, PkceGenerator.CreateChallenge(exchange.Verifier)); // el verifier corresponde al challenge enviado

        var profile = await client.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains(TestJwt.ClientUserId.ToString(), await profile.ReadHtmlAsync());
    }

    [Fact]
    public async Task Callback_WrongState_IsRejected_WithoutExchangingNorOpeningASession()
    {
        var (client, _, _) = await StartedAsync();
        _factory.LoginClient.Codes["cod"] = TestJwt.ForClient();

        var response = await client.GetAsync("/Account/Callback?code=cod&state=otro-state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("venció o no es válido", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
        Assert.Empty(_factory.LoginClient.Exchanges);
    }

    [Fact]
    public async Task Callback_WithoutAnInitiatedFlow_IsRejected()
    {
        _factory.LoginClient.Codes["cod"] = TestJwt.ForClient();

        var response = await _factory.CreateBrowserClient().GetAsync("/Account/Callback?code=cod&state=cualquiera");

        Assert.Contains("venció o no es válido", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
        Assert.Empty(_factory.LoginClient.Exchanges);
    }

    [Fact]
    public async Task Callback_FlowIsSingleUse_ReplayingTheSameReturnIsRejected()
    {
        var (client, state, _) = await StartedAsync();
        _factory.LoginClient.Codes["cod"] = TestJwt.ForClient();
        var url = $"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}";

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(url)).StatusCode);
        var replay = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("venció o no es válido", await replay.ReadHtmlAsync());
        Assert.Single(_factory.LoginClient.Exchanges);
    }

    [Fact]
    public async Task Callback_TamperedFlowCookie_IsRejected()
    {
        var (_, state, _) = await StartedAsync();
        var client = _factory.CreateBrowserClient(); // otro navegador, sin la cookie del inicio
        client.DefaultRequestHeaders.Add("Cookie", $"{ExternalLoginFlow.CookieName}=CfDJ8-valor-manipulado");

        var response = await client.GetAsync($"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}");

        Assert.Contains("venció o no es válido", await response.ReadHtmlAsync());
        Assert.Empty(_factory.LoginClient.Exchanges);
    }

    [Fact]
    public async Task Callback_AuthServerRejectsTheCode_ShowsAGenericError_WithoutSession()
    {
        var (client, state, _) = await StartedAsync(); // el código no está registrado en el fake: inválido o ya usado

        var response = await client.GetAsync($"/Account/Callback?code=usado&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo completar el ingreso", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
    }

    [Fact]
    public async Task Callback_ErrorParameterFromTheAuthServer_IsRejected_WithoutExchanging()
    {
        var (client, state, _) = await StartedAsync();

        var response = await client.GetAsync($"/Account/Callback?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Contains("venció o no es válido", await response.ReadHtmlAsync());
        Assert.Empty(_factory.LoginClient.Exchanges);
    }

    [Fact]
    public async Task Callback_AuthServerDown_ShowsTheUnavailableMessage()
    {
        var (client, state, _) = await StartedAsync();
        _factory.LoginClient.Throw = new AuthServerUnavailableException("caído");

        var response = await client.GetAsync($"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
    }

    [Fact]
    public async Task Callback_ServiceRoleToken_IsNotAllowed()
    {
        var (client, state, _) = await StartedAsync();
        _factory.LoginClient.Codes["cod"] = TestJwt.ForService();

        var response = await client.GetAsync($"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}");

        Assert.Contains("no tiene permiso", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
    }

    // ---------- returnUrl ----------

    [Fact]
    public async Task Callback_Success_ReturnsToTheLocalReturnUrl()
    {
        var (client, state, _) = await StartedAsync(returnUrl: "/Account/Profile");
        _factory.LoginClient.Codes["cod"] = TestJwt.ForClient();

        var response = await client.GetAsync($"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}");

        Assert.Equal("/Account/Profile", response.RedirectLocation());
    }

    [Fact]
    public async Task Start_ExternalReturnUrl_IsIgnored_NoOpenRedirect()
    {
        var (client, state, _) = await StartedAsync(returnUrl: "https://evil.example/robo");
        _factory.LoginClient.Codes["cod"] = TestJwt.ForClient();

        var response = await client.GetAsync($"/Account/Callback?code=cod&state={Uri.EscapeDataString(state)}");

        Assert.Equal("/", response.RedirectLocation());
    }
}
