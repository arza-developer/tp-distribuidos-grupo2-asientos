using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SeatsReservation.Auth;

// Requisito 3.e: punto único para que cada microservicio verifique los tokens del AuthServer.
public static class JwtAuthenticationExtensions
{
    // Los mismos valores que pone el AuthServer al emitir. Si no coinciden, el token se rechaza.
    public const string Issuer = "SeatsReservation.AuthServer";
    public const string Audience = "SeatsReservation.Microservices";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sin esto, .NET renombra "role" y "sub" a nombres largos y [Authorize(Roles=...)] no los encuentra.
                options.MapInboundClaims = false;

                // En el TP corremos en HTTP, sin certificados.
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // ¿Lo emitió NUESTRO AuthServer?
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,

                    // ¿Es para NUESTROS microservicios?
                    ValidateAudience = true,
                    ValidAudience = Audience,

                    // ¿Está vencido? (30 segundos de tolerancia entre relojes)
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    // ¿La firma es auténtica? Recalcula el sello con la clave y compara.
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

                    // Le dice a .NET qué claim es el nombre y cuál es el rol.
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();
        return services;
    }
}