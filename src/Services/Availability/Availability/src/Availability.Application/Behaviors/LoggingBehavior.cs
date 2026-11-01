using Availability.Application.Abstractions;
using Availability.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Availability.Application.Behaviors;

/// <summary>
/// Requisito 3.f (logs de auditoría con inicio y fin de la cadena de sucesos): cada caso de uso (comando o query de
/// MediatR) registra <c>INICIO:</c> y <c>FIN:</c> con su duración. El CorrelationId viaja como propiedad del log
/// (scope), de modo que la salida es <c>[{CorrelationId}] INICIO: …</c> / <c>[{CorrelationId}] FIN: …</c>.
/// <para>
/// El FIN con error NO adjunta la excepción: de su traza ya se ocupan <c>ApiExceptionHandler</c> (negocio/técnicas) y
/// <c>ExceptionHandlerMiddleware</c> (inesperadas); aquí solo se deja constancia de cómo terminó el caso de uso.
/// Error de negocio → Información; cualquier otra excepción → Error. La excepción siempre se relanza.
/// </para>
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICorrelationIdAccessor correlation,
    TimeProvider clock) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var started = clock.GetTimestamp();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlation.CorrelationId,
            ["Request"] = name
        });

        // {@Datos} ya imprime el nombre del tipo de la solicitud; el nombre también queda como propiedad (scope).
        logger.LogInformation("INICIO: {@Datos}", request);

        try
        {
            var response = await next();
            logger.LogInformation("FIN: {Request:l} ({ElapsedMs:F0} ms)", name, Elapsed(started));
            return response;
        }
        catch (BusinessException ex)
        {
            logger.LogInformation("FIN: {Request:l} con error de negocio {Code:l} ({ElapsedMs:F0} ms)",
                name, ex.Code, Elapsed(started));
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("FIN: {Request:l} con error {ErrorType:l} ({ElapsedMs:F0} ms)",
                name, ex.GetType().Name, Elapsed(started));
            throw;
        }
    }

    private double Elapsed(long started) => clock.GetElapsedTime(started).TotalMilliseconds;
}
