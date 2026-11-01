using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SeatsReservation.Observability;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var extractedId) && !string.IsNullOrWhiteSpace(extractedId))
        {
            correlationId = extractedId!;
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeaderName] = correlationId;
        }

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}