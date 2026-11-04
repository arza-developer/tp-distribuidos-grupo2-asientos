using System.IdentityModel.Tokens.Jwt;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Availability.Tests.Support;
using Availability.Web.Mvc.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeatsReservation.Auth;

namespace Availability.Tests.Mvc;

/// <summary>
/// El servicio valida el token del Auth Server con los MISMOS parámetros que la API: se toman de la configuración real de
/// <c>AddJwtAuthentication</c> (la copia de la librería compartida), no se reimplementan en el test.
/// </summary>
public class SessionLoginServiceTests
{
    private sealed class StubLoginClient(string? token) : IAuthServerLoginClient
    {
        public Exception? Throw { get; init; }

        public Task<string?> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default) =>
            Throw is not null ? throw Throw : Task.FromResult(token);

        public Task<string?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId,
            string correlationId, CancellationToken ct = default) =>
            Throw is not null ? throw Throw : Task.FromResult(token);
    }

    private static IOptionsMonitor<JwtBearerOptions> RealJwtOptions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = TestJwt.Key }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(config);
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
    }

    private static SessionLoginService Create(string? token, Exception? throws = null) =>
        new(new StubLoginClient(token) { Throw = throws }, RealJwtOptions(), NullLogger<SessionLoginService>.Instance);

    [Fact]
    public async Task ValidClientToken_Succeeds_WithOnlySubRoleAndNameClaims()
    {
        var result = await Create(TestJwt.ForClient()).LoginAsync("cliente1", "x", "c");

        Assert.Equal(LoginStatus.Success, result.Status);
        var claims = result.Principal!.Claims.ToDictionary(c => c.Type, c => c.Value);
        Assert.Equal(["name", "role", "sub"], claims.Keys.Order().ToArray()); // el JWT no se conserva ni se copian otros claims
        Assert.Equal(TestJwt.ClientUserId.ToString(), claims["sub"]);
        Assert.Equal("Client", claims["role"]);
        Assert.Equal("cliente1", claims["name"]);
        Assert.Equal("cliente1", result.Principal.Identity!.Name);
        Assert.True(result.Principal.IsInRole("Client"));
        Assert.True(result.Principal.Identity.IsAuthenticated);
    }

    [Fact]
    public async Task Session_LastsAsLongAsTheJwt()
    {
        var expires = DateTime.UtcNow.AddMinutes(20);
        var token = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", expires: expires);

        var result = await Create(token).LoginAsync("cliente1", "x", "c");

        var expected = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
        Assert.Equal(new DateTimeOffset(expected, TimeSpan.Zero), result.ExpiresUtc);
        Assert.InRange((result.ExpiresUtc!.Value - DateTimeOffset.UtcNow).TotalMinutes, 19, 20.1);
    }

    [Fact]
    public async Task NullToken_MeansInvalidCredentials()
    {
        var result = await Create(token: null).LoginAsync("cliente1", "mala", "c");

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Principal);
    }

    [Theory]
    [InlineData("Service")]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task OtherRoles_AreNotAllowed(string role)
    {
        var token = TestJwt.Create(TestJwt.ClientUserId.ToString(), role);

        var result = await Create(token).LoginAsync("u", "x", "c");

        Assert.Equal(LoginStatus.RoleNotAllowed, result.Status);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task ClientRoleWithNonGuidSubject_IsNotAllowed()
    {
        var result = await Create(TestJwt.Create("availability-api", "Client")).LoginAsync("u", "x", "c");

        Assert.Equal(LoginStatus.RoleNotAllowed, result.Status);
    }

    [Fact]
    public async Task ForgedExpiredOrForeignTokens_AreInvalid()
    {
        var forged = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", key: "otra-clave-distinta-de-al-menos-32-bytes!!");
        var expired = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client",
            notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2));
        var wrongIssuer = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", issuer: "OtroEmisor");
        var wrongAudience = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", audience: "OtraAudiencia");

        foreach (var token in new[] { forged, expired, wrongIssuer, wrongAudience, "esto.no.es-un-jwt" })
            Assert.Equal(LoginStatus.InvalidToken, (await Create(token).LoginAsync("u", "x", "c")).Status);
    }

    [Fact]
    public async Task VeryLongUserNames_AreTruncatedInTheNameClaim()
    {
        var result = await Create(TestJwt.ForClient()).LoginAsync(new string('x', 500), "x", "c");

        Assert.Equal(100, result.Principal!.Identity!.Name!.Length);
    }

    [Fact]
    public async Task AuthServerFailure_Propagates_ToTheController()
    {
        var service = Create(token: null, throws: new AuthServerUnavailableException("caído"));

        await Assert.ThrowsAsync<AuthServerUnavailableException>(() => service.LoginAsync("u", "x", "c"));
    }

    // ---------- Login federado: canje del código (Fase 8) ----------

    [Fact]
    public async Task Code_ValidClientToken_Succeeds_WithTheSubjectAsName()
    {
        var result = await Create(TestJwt.ForClient()).LoginWithCodeAsync("cod", "ver", "http://x/cb", "frontend", "c");

        Assert.Equal(LoginStatus.Success, result.Status);
        var claims = result.Principal!.Claims.ToDictionary(c => c.Type, c => c.Value);
        Assert.Equal(["name", "role", "sub"], claims.Keys.Order().ToArray());
        Assert.Equal(TestJwt.ClientUserId.ToString(), claims["sub"]);
        Assert.Equal(TestJwt.ClientUserId.ToString(), claims["name"]); // el JWT no trae nombre de usuario
        Assert.True(result.Principal.IsInRole("Client"));
    }

    [Fact]
    public async Task Code_RejectedByTheAuthServer_IsInvalidCode()
    {
        var result = await Create(token: null).LoginWithCodeAsync("usado", "ver", "http://x/cb", "frontend", "c");

        Assert.Equal(LoginStatus.InvalidCode, result.Status);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Code_ServiceRole_IsNotAllowed()
    {
        var result = await Create(TestJwt.ForService()).LoginWithCodeAsync("cod", "ver", "http://x/cb", "frontend", "c");

        Assert.Equal(LoginStatus.RoleNotAllowed, result.Status);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Code_ForgedOrExpiredToken_IsInvalid()
    {
        var forged = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client", key: "otra-clave-distinta-de-al-menos-32-bytes!!");
        var expired = TestJwt.Create(TestJwt.ClientUserId.ToString(), "Client",
            notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2));

        foreach (var token in new[] { forged, expired })
            Assert.Equal(LoginStatus.InvalidToken, (await Create(token).LoginWithCodeAsync("c", "v", "http://x/cb", "frontend", "c")).Status);
    }

    [Fact]
    public async Task Code_NonGuidSubject_IsNotAllowed()
    {
        var result = await Create(TestJwt.Create("availability", "Client")).LoginWithCodeAsync("c", "v", "http://x/cb", "frontend", "c");

        Assert.Equal(LoginStatus.RoleNotAllowed, result.Status);
    }

    [Fact]
    public async Task Code_AuthServerFailure_Propagates()
    {
        var service = Create(token: null, throws: new AuthServerUnavailableException("caído"));

        await Assert.ThrowsAsync<AuthServerUnavailableException>(() => service.LoginWithCodeAsync("c", "v", "http://x/cb", "frontend", "c"));
    }
}
