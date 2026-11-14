using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Availability.Application.Exceptions;
using Availability.Tests.Api;
using Availability.Tests.Support;

namespace Availability.Tests.Mvc;

/// <summary>Login y sesión por cookie de las páginas MVC (T5.1).</summary>
public class AccountTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public AccountTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.LoginClient.Reset();
        _factory.Publisher.Reset();
    }

    // ---------- formulario ----------

    [Fact]
    public async Task LoginPage_Renders_FormWithAntiforgeryToken_AndNoPasswordValue()
    {
        var html = await _factory.CreateBrowserClient().GetStringAsync("/Account/Login");

        Assert.Contains("<form", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("name=\"Username\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.DoesNotContain("value=\"cliente1-dev\"", html);
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_ValidClient_RedirectsHome_AndSetsAHardenedSessionCookie()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.RedirectLocation());
        var cookie = response.SessionSetCookie();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase); // HTTP de laboratorio: SameAsRequest (limitación M7)
    }

    [Fact]
    public async Task Login_SendsTheCredentialsAndTheCorrelationIdToTheAuthServer()
    {
        var client = _factory.CreateBrowserClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "login-corr-1");

        await client.LoginAsync();

        var call = Assert.Single(_factory.LoginClient.Calls);
        Assert.Equal(("cliente1", "cliente1-dev"), (call.User, call.Password));
        Assert.Equal("login-corr-1", call.CorrelationId);
    }

    [Fact]
    public async Task Login_Session_AllowsAccessToProtectedPages_AndShowsTheIdentity()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();

        var page = await client.GetAsync("/Account/Profile");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("cliente1", html);
        Assert.Contains("Client", html);
        Assert.Contains(TestJwt.ClientUserId.ToString(), html);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns200WithMessage_AndNoCookie_AndNeverEchoesThePassword()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync("cliente1", "mala-clave");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Usuario o contraseña incorrectos.", html);
        Assert.Null(response.SessionSetCookie());
        Assert.DoesNotContain("mala-clave", html);
        Assert.Contains("cliente1", html); // el usuario se conserva para reintentar
    }

    [Fact]
    public async Task Login_UnknownUser_GivesTheSameMessageAsAWrongPassword()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync("nadie", "x");

        Assert.Contains("Usuario o contraseña incorrectos.", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
    }

    [Fact]
    public async Task Login_AccountWithServiceRole_IsRejected_AndNoCookie()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync("servicio", "pw");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no tiene permiso para reservar", await response.ReadHtmlAsync());
        Assert.Null(response.SessionSetCookie());
    }

    [Fact]
    public async Task Login_TokenFromAnotherIssuerOrKey_IsRejected_AsInvalidCredentials()
    {
        _factory.LoginClient.Tokens[("falsificado", "pw")] =
            TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", key: "otra-clave-distinta-de-al-menos-32-bytes!!");
        _factory.LoginClient.Tokens[("expirado", "pw")] =
            TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2));
        _factory.LoginClient.Tokens[("sinsub", "pw")] = TestJwt.Create("no-es-un-guid", "Client");

        foreach (var user in new[] { "falsificado", "expirado", "sinsub" })
        {
            var response = await _factory.CreateBrowserClient().LoginAsync(user, "pw");

            Assert.Null(response.SessionSetCookie());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_AuthServerDown_ShowsAFriendlyMessage_WithoutInternals_AndNoCookie()
    {
        _factory.LoginClient.Throw = new AuthServerUnavailableException("secreto-interno", new HttpRequestException("detalle-red"));

        var response = await _factory.CreateBrowserClient().LoginAsync();
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("servicio de autenticación no está disponible", html);
        Assert.DoesNotContain("secreto-interno", html);
        Assert.DoesNotContain("detalle-red", html);
        Assert.Null(response.SessionSetCookie());
    }

    [Fact]
    public async Task Login_EmptyFields_AreValidated_WithoutCallingTheAuthServer()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync("", "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ingresá el usuario.", await response.ReadHtmlAsync());
        Assert.Empty(_factory.LoginClient.Calls);
    }

    // ---------- redirecciones ----------

    [Fact]
    public async Task Login_HonorsALocalReturnUrl()
    {
        var response = await _factory.CreateBrowserClient().LoginAsync(returnUrl: "/Account/Profile");

        Assert.Equal("/Account/Profile", response.RedirectLocation());
    }

    [Theory]
    [InlineData("https://evil.test/robo")]
    [InlineData("//evil.test/robo")]
    [InlineData("/\\evil.test")]
    [InlineData("javascript:alert(1)")]
    public async Task Login_IgnoresNonLocalReturnUrls_PreventingOpenRedirects(string returnUrl)
    {
        var response = await _factory.CreateBrowserClient().LoginAsync(returnUrl: returnUrl);

        Assert.Equal("/", response.RedirectLocation());
    }

    [Fact]
    public async Task ProtectedPage_WithoutSession_RedirectsToLogin_WithReturnUrl()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/Account/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login?ReturnUrl=%2FAccount%2FProfile", response.RedirectLocation());
    }

    // ---------- antiforgery ----------

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_Returns400_AndNeverCallsTheAuthServer()
    {
        var client = _factory.CreateBrowserClient();
        await client.GetStringAsync("/Account/Login"); // obtiene la cookie antiforgery pero no manda el token

        var response = await client.PostFormAsync("/Account/Login",
            new Dictionary<string, string> { ["Username"] = "cliente1", ["Password"] = "cliente1-dev" }, antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.LoginClient.Calls);
    }

    [Fact]
    public async Task Login_WithTokenFromAnotherSession_Returns400()
    {
        var other = _factory.CreateBrowserClient();
        var foreignToken = await other.GetAntiforgeryTokenAsync("/Account/Login");
        var attacker = _factory.CreateBrowserClient();
        await attacker.GetStringAsync("/Account/Login");

        var response = await attacker.PostFormAsync("/Account/Login",
            new Dictionary<string, string> { ["Username"] = "cliente1", ["Password"] = "cliente1-dev" }, foreignToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- logout ----------

    [Fact]
    public async Task Logout_ClearsTheSession_AndProtectedPagesRedirectAgain()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();
        var token = await client.GetAntiforgeryTokenAsync("/Account/Profile"); // el logout está en el layout

        var logout = await client.PostFormAsync("/Account/Logout", [], token);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/Account/Login", logout.RedirectLocation());
        Assert.Contains("expires=Thu, 01 Jan 1970", logout.SessionSetCookie()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryToken_Returns400_AndKeepsTheSession()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();

        var logout = await client.PostFormAsync("/Account/Logout", [], antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode); // evita cerrar la sesión por un POST cruzado
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    // ---------- codificación HTML ----------

    [Fact]
    public async Task UserNameInHtml_IsEncoded_NeverRenderedAsMarkup()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync(FakeAuthServerLoginClient.HtmlUser, "pw");

        var html = await client.GetStringAsync("/Account/Profile");

        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
    }

    // ---------- aislamiento cookie ↔ Bearer ----------

    [Fact]
    public async Task SessionCookie_DoesNotAuthenticateTheApi_ItAnswers401Json_NotARedirect()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();

        var response = await client.PostAsJsonAsync("/api/reservations",
            new { eventId = Guid.NewGuid(), seatNumber = "A-1", userId = TestJwt.ClientUserId, price = 1m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("No autenticado", (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["title"].ToString());
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task BearerToken_DoesNotOpenAPageSession_ItRedirectsToLogin()
    {
        var client = _factory.CreateBrowserClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.ForClient());

        var response = await client.GetAsync("/Account/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.RedirectLocation()!);
    }

    [Fact]
    public async Task Api_KeepsWorkingWithBearer_WhenTheSameClientAlsoHasASessionCookie()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.ForClient());

        var seat = await _factory.CreateAvailableSeatAsync();
        var response = await client.PostAsJsonAsync("/api/reservations",
            new { eventId = Availability.Infrastructure.Persistence.SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // ---------- acceso denegado ----------

    [Fact]
    public async Task DeniedPage_Returns403WithAMessage()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/Account/Denied");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Acceso denegado", await response.ReadHtmlAsync());
    }

    // ---------- logs ----------

    [Fact]
    public async Task Login_NeverWritesThePasswordToTheLogs()
    {
        var client = _factory.CreateBrowserClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "login-corr-pw");

        await client.LoginAsync("cliente1", "mala-clave-secreta");

        var rendered = _factory.Logs.For("login-corr-pw").Select(e => e.RenderMessage());
        Assert.DoesNotContain(rendered, m => m.Contains("mala-clave-secreta"));
    }
}
