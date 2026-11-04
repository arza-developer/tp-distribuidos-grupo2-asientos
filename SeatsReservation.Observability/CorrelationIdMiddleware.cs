using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SeatsReservation.Observability;

/// <summary>
/// Correlación transversal (req. 3.f): toma el <c>X-Correlation-ID</c> entrante (o genera uno), lo deja en el request y lo
/// devuelve en la respuesta, lo publica en <c>HttpContext.Items["CorrelationId"]</c> para que el código de cada servicio
/// lo lea sin releer headers, y lo agrega a todos los logs de la request (<c>LogContext</c>).
/// <para>
/// El valor entrante se acepta solo si es corto y de caracteres seguros (evita inyección en los logs); si no, se genera
/// uno nuevo.
/// </para>
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>Clave en <c>HttpContext.Items</c> con el id de la request.</summary>
    public const string ItemKey = "CorrelationId";

    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        if (IsAcceptable(incoming))
        {
            correlationId = incoming!;
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;
        }

        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = correlationId;
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
