using Availability.Application.Abstractions;
using Availability.Web.Middleware;

namespace Availability.Web.Services;

/// <summary>
/// Correlación por request (req. 3.f): el id que fijó <see cref="CorrelationIdMiddleware"/> (header
/// <c>X-Correlation-ID</c> entrante o generado). Si se usa fuera de una request, cae a <c>TraceIdentifier</c> o a un id nuevo.
/// </summary>
public sealed class HttpCorrelationIdAccessor(IHttpContextAccessor http) : ICorrelationIdAccessor
{
    public string CorrelationId =>
        http.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string
        ?? http.HttpContext?.TraceIdentifier
        ?? Guid.NewGuid().ToString("N");
}
