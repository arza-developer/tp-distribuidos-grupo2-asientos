using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Support;

namespace Availability.Tests.Api;

/// <summary>Requisito 3.e: autenticación con tokens del Auth Server; catálogo y /health públicos; reservas solo para rol Client.</summary>
public class AuthenticationTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public AuthenticationTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
    }

    private HttpClient WithToken(string token) => _factory.CreateAuthenticatedClient(token);

    private async Task<object> BodyAsync() => new
    {
        eventId = SeedData.EventId,
        seatNumber = await _factory.CreateAvailableSeatAsync(),
        userId = TestJwt.ClientUserId,
        price = 15000m
    };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    // ---------- endpoints públicos ----------

    [Fact]
    public async Task PublicEndpoints_WorkWithoutToken()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/seats")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);
    }

    // ---------- sin credenciales ----------

    [Fact]
    public async Task Post_WithoutToken_Returns401WithProblemDetails_AndBearerChallenge()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        var json = await ReadAsync(response);
        Assert.Equal(401, json.GetProperty("status").GetInt32());
        Assert.Equal("No autenticado", json.GetProperty("title").GetString());
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401_WithoutCallingBooking()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/reservations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Booking.Calls);
    }

    // ---------- token válido ----------

    [Fact]
    public async Task Post_WithValidClientToken_Returns202()
    {
        var response = await WithToken(TestJwt.ForClient()).PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithValidClientToken_PassesAuthentication()
    {
        var response = await WithToken(TestJwt.ForClient()).GetAsync($"/api/reservations/{Guid.NewGuid()}");

        // Autenticado: llega al caso de uso, que no encuentra la reserva.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- rol equivocado ----------

    [Theory]
    [InlineData("Service")]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task Post_WithTokenOfAnotherRole_Returns403WithProblemDetails(string role)
    {
        var token = TestJwt.Create(TestJwt.ClientUserId.ToString(), role);

        var response = await WithToken(token).PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Acceso denegado", (await ReadAsync(response)).GetProperty("title").GetString());
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Post_WithServiceToken_Returns403_ServicesCannotReserveForUsers()
    {
        var response = await WithToken(TestJwt.ForService()).PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- tokens inválidos ----------

    [Fact]
    public async Task Post_WithExpiredToken_Returns401()
    {
        var expired = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client",
            notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2));

        var response = await WithToken(expired).PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithTokenSignedByAnotherKey_Returns401()
    {
        var forged = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", key: "otra-clave-distinta-de-al-menos-32-bytes!!");

        var response = await WithToken(forged).PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithWrongIssuer_Returns401()
    {
        var token = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", issuer: "OtroEmisor");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await WithToken(token).PostAsJsonAsync("/api/reservations", await BodyAsync())).StatusCode);
    }

    [Fact]
    public async Task Post_WithWrongAudience_Returns401()
    {
        var token = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", audience: "OtraAudiencia");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await WithToken(token).PostAsJsonAsync("/api/reservations", await BodyAsync())).StatusCode);
    }

    [Fact]
    public async Task Post_WithGarbageToken_Returns401()
    {
        var response = await WithToken("esto.no.es-un-jwt").PostAsJsonAsync("/api/reservations", await BodyAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithNonBearerScheme_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNz");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/reservations", await BodyAsync())).StatusCode);
    }
}
