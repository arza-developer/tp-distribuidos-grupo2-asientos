using Availability.Application.Exceptions;
using Availability.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Middleware;

/// <summary>
/// Traduce excepciones a ProblemDetails por CATEGORÍA (el HTTP es una preocupación de esta capa, no del dominio):
/// negocio inexistente → 404, negocio inválido → 400, conflicto de estado → 409, fallo técnico de mensajería → 503.
/// Cualquier otra excepción no se maneja aquí y termina como 500 genérico, sin detalles.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case BusinessNotFoundException e:
                logger.LogInformation("Negocio ({Code}): {Message}", e.Code, e.Message);
                problem = Business(StatusCodes.Status404NotFound, "Recurso no encontrado", e);
                break;

            case BusinessValidationException e:
                logger.LogInformation("Negocio ({Code}): {Message}", e.Code, e.Message);
                problem = Business(StatusCodes.Status400BadRequest, "Solicitud inválida", e);
                break;

            case BusinessConflictException e:
                logger.LogInformation("Negocio ({Code}): {Message}", e.Code, e.Message);
                problem = Business(StatusCodes.Status409Conflict, "Conflicto con el estado del recurso", e);
                break;

            case MessagePublishException e:
                logger.LogError(e, "Fallo técnico de mensajería");
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Servicio no disponible",
                    Detail = "No se pudo encolar la solicitud. Intente nuevamente más tarde."
                };
                break;

            default:
                return false;
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static ProblemDetails Business(int status, string title, BusinessException e) => new()
    {
        Status = status,
        Title = title,
        Detail = e.Message,
        Extensions = { ["code"] = e.Code }
    };
}
