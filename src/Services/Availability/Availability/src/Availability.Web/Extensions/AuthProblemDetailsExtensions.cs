using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Extensions;

/// <summary>
/// Respuestas 401/403 con <see cref="ProblemDetails"/>, consistentes con el resto de los errores de la API.
/// Se agrega fuera de la copia de la librería compartida (que no las provee) para poder eliminarla sin perderlas.
/// </summary>
public static class AuthProblemDetailsExtensions
{
    public static IServiceCollection AddAuthProblemDetails(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse(); // evita la respuesta 401 vacía por defecto
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "No autenticado",
                        Detail = "Se requiere un token Bearer válido emitido por el servidor de autorización."
                    });
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Acceso denegado",
                        Detail = "El token no tiene permisos para este recurso."
                    });
                }
            };
        });
        return services;
    }
}
