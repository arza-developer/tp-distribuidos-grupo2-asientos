using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Availability.Tests.Support;

/// <summary>
/// Genera tokens con la MISMA forma que <c>TokenService.CreateToken</c> del Auth Server (rama feature/06-auth-server-m2m,
/// commit 67a7e32): HS256, claims <c>sub</c>, <c>jti</c> y <c>role</c>, issuer/audience fijos y 3600 s de vida.
/// Si el Auth Server cambia ese formato, los tests de compatibilidad (<c>TokenCompatibilityTests</c>) lo delatan (TD-7).
/// </summary>
public static class TestJwt
{
    // Los mismos valores que usa el Auth Server en desarrollo.
    public const string Key = "SeatsReservation-Dev-Key-Change-Me-Min-32-Bytes!!";
    public const string Issuer = "SeatsReservation.AuthServer";
    public const string Audience = "SeatsReservation.Microservices";
    public const int ExpiresInSeconds = 3600;

    /// <summary>UserId del usuario de prueba "cliente1" del Auth Server.</summary>
    public static readonly Guid ClientUserId = Guid.Parse("7bb85f64-1111-2222-3333-2c963f66af99");

    public static string Create(
        string subject, string role, string? key = null, string? issuer = null, string? audience = null,
        DateTime? expires = null, DateTime? notBefore = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", role)
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires ?? DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Key)), SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>Token de un usuario (rol Client, sub = UserId Guid).</summary>
    public static string ForClient(Guid? userId = null) => Create((userId ?? ClientUserId).ToString(), "Client");

    /// <summary>Token de un servicio (rol Service, sub = ClientId), como el que pide Availability para llamar a Booking.</summary>
    public static string ForService(string clientId = "availability-api") => Create(clientId, "Service");
}
