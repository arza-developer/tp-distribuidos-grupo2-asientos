using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace Availability.Web.Middleware;

/// <summary>
/// Requisito 3.f: toma el <c>X-Correlation-ID</c> entrante (o genera uno), lo devuelve en la respuesta, lo deja en
/// <c>HttpContext.Items</c> y lo agrega a todos los logs de la request (<c>LogContext</c>).
/// Debe ir ANTES del manejador de excepciones para que también sus logs lleven el id.
/// <para>
/// El valor entrante se acepta solo si es corto y de caracteres seguros (evita inyección en los logs); si no, se genera
/// uno nuevo. El header de respuesta se fija en <c>OnStarting</c> porque el manejador de excepciones limpia la respuesta.
/// </para>
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = Resolve(context.Request.Headers[HeaderName]);
        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }

    private static string Resolve(StringValues header)
    {
        var candidate = header.FirstOrDefault();
        return IsAcceptable(candidate) ? candidate! : Guid.NewGuid().ToString("N");
    }

    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
