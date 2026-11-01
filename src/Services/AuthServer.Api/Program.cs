using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SeatsReservation.Observability; // <-- Namespace de tu librería de observabilidad

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Serilog para leer desde appsettings.json y capturar logs estructurados
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Registrar los servicios de observabilidad transversales (LogContext, etc.)
builder.Services.AddSeatsObservability();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// 3. Registrar el Middleware de Correlación al comienzo absoluto del pipeline HTTP
app.UseSeatsObservability();

// [Decisión de Seguridad]: Clave simétrica compartida (HS256) para validación local de ultra-baja latencia
const string SecretKey = "SeatsReservation-Super-Secret-Key-University-Project-2026!";

// [Paso 2 y Requisito 3.e]: Endpoint de emisión de tokens JWT para usuarios y microservicios (M2M)
app.MapPost("/connect/token", (TokenRequestDto dto) =>
{
    // Opcional: Puedes usar Log.Information aquí y el CorrelationId saldrá automáticamente impreso
    Serilog.Log.Information("INICIO: Emitiendo token JWT para el cliente {ClientId}", dto.ClientId ?? "anonymous-user");

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

    Serilog.Log.Information("FIN: Token JWT emitido exitosamente para {ClientId}", dto.ClientId ?? "anonymous-user");

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

app.MapGet("/", () => "Auth Server running. Use POST /connect/token to get JWT.");

app.Run();

/// <summary>
/// [Dominio Anémico - Requisito 3.e]: DTO de solicitud de token de autenticación.
/// </summary>
public record TokenRequestDto(string? ClientId, string? ClientSecret, string? Role);