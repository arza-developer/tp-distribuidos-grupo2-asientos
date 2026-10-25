using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// [Decisión de Seguridad]: Clave simétrica compartida (HS256) para validación local de ultra-baja latencia
const string SecretKey = "SeatsReservation-Super-Secret-Key-University-Project-2026!";

// [Paso 2 y Requisito 3.e]: Endpoint de emisión de tokens JWT para usuarios y microservicios (M2M)
app.MapPost("/connect/token", (TokenRequestDto dto) =>
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(SecretKey);

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
})
.WithName("GetToken")
.WithSummary("[Paso 2 y 3.e]: Emisión de tokens de autenticación JWT")
.WithDescription("Genera tokens simétricos HS256 tanto para usuarios finales como para comunicación servicio a servicio (M2M).");

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

app.Run();

/// <summary>
/// [Dominio Anémico - Requisito 3.e]: DTO de solicitud de token de autenticación.
/// </summary>
public record TokenRequestDto(string? ClientId, string? ClientSecret, string? Role);
