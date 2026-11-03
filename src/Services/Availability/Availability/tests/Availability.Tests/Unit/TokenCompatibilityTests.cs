using System.IdentityModel.Tokens.Jwt;
using Availability.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeatsReservation.Auth;

namespace Availability.Tests.Unit;

/// <summary>
/// Fija el contrato con el Auth Server (rama feature/06-auth-server-m2m, commit 67a7e32): constantes, formato de los
/// tokens que emite <c>TokenService.CreateToken</c> y comportamiento de la librería compartida (copia local, TD-6/TD-7).
/// </summary>
public class TokenCompatibilityTests
{
    [Fact]
    public void Constants_MatchTheAuthServer()
    {
        Assert.Equal("SeatsReservation.AuthServer", JwtAuthenticationExtensions.Issuer);
        Assert.Equal("SeatsReservation.Microservices", JwtAuthenticationExtensions.Audience);
        Assert.Equal(TestJwt.Issuer, JwtAuthenticationExtensions.Issuer);
        Assert.Equal(TestJwt.Audience, JwtAuthenticationExtensions.Audience);
    }

    [Fact]
    public void ClientToken_HasTheExactShapeTheAuthServerIssues()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(TestJwt.ForClient());

        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal(TestJwt.ClientUserId.ToString(), jwt.Subject);
        Assert.Equal("Client", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.True(Guid.TryParse(jwt.Id, out _)); // jti
        Assert.Equal(TestJwt.Issuer, jwt.Issuer);
        Assert.Equal(TestJwt.Audience, Assert.Single(jwt.Audiences));
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalSeconds, 3500, 3601); // 3600 s de vida
        // sub/jti/role + iss/aud/exp, más iat/nbf que JwtSecurityTokenHandler agrega por defecto (igual que el Auth Server).
        Assert.Equal(["aud", "exp", "iat", "iss", "jti", "nbf", "role", "sub"],
            jwt.Claims.Select(c => c.Type).Distinct().Order().ToArray());
    }

    [Fact]
    public void ServiceToken_UsesClientIdAsSubject_AndServiceRole()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(TestJwt.ForService("availability-api"));

        Assert.Equal("availability-api", jwt.Subject);
        Assert.Equal("Service", jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public void AddJwtAuthentication_WithoutJwtKey_FailsFast()
    {
        var emptyConfig = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(emptyConfig));

        Assert.Contains("Jwt:Key", ex.Message);
    }
}
