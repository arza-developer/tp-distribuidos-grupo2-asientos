using Availability.Domain.Exceptions;
using Serilog.Events;

namespace Availability.Web.Logging;

public static class LogFilters
{
    /// <summary>
    /// Los errores de negocio (404/409/403…) ya los traduce y loguea <c>ApiExceptionHandler</c> en nivel Información;
    /// <c>ExceptionHandlerMiddleware</c> los repite como <c>fail:</c>. Se excluye ESA repetición (solo para
    /// <see cref="BusinessException"/>) sin ocultar los 500 reales.
    /// </summary>
    public static bool IsHandledBusinessException(LogEvent logEvent) =>
        logEvent.Exception is BusinessException
        && logEvent.Properties.TryGetValue("SourceContext", out var source)
        && source.ToString().Contains("ExceptionHandlerMiddleware", StringComparison.Ordinal);
}
