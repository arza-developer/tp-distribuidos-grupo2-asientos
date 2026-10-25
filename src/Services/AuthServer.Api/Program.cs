using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// [Decisión de Seguridad]: Clave simétrica compartida (HS256) para validación local de ultra-baja latencia
const string SecretKey = "SeatsReservation-Super-Secret-Key-University-Project-2026!";

/// <summary>
/// [Paso 2 y Requisito 3.e]: Servidor de Autorización OAuth2 / OpenID Connect.
/// Emite tokens JWT tanto para usuarios finales como para comunicación servicio a servicio (M2M).
/// </summary>
app.MapPost("/connect/token", (TokenRequestDto dto) =>
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(SecretKey);

    // [Claims Estándar RFC 7519]: Identidad del sujeto, identificador único y roles
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, dto.ClientId ?? "anonymous-user"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new("role", dto.Role ?? "Client")
    };

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddHours(2),
        Issuer = "SeatsReservation.AuthServer",
        Audience = "SeatsReservation.Microservices",
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var jwtString = tokenHandler.WriteToken(token);

    return Results.Ok(new
    {
        access_token = jwtString,
        token_type = "Bearer",
        expires_in = 7200
    });
});

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

app.Run();

public record TokenRequestDto(string? ClientId, string? ClientSecret, string? Role);
