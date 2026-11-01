using Availability.Application.Abstractions;

namespace Availability.Web.Services;

/// <summary>
/// Correlación por request. Por ahora usa <c>HttpContext.TraceIdentifier</c> (igual que el stub actual);
/// T4.2 lo reemplaza por el header X-Correlation-ID.
/// </summary>
public sealed class HttpCorrelationIdAccessor(IHttpContextAccessor http) : ICorrelationIdAccessor
{
    public string CorrelationId => http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}
