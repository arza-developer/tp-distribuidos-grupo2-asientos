using Availability.Application.Exceptions;
using Availability.Domain.Exceptions;
using Serilog.Events;

namespace Availability.Web.Logging;

public static class LogFilters
{
    /// <summary>
    /// <c>ApiExceptionHandler</c> ya traduce y loguea las excepciones que conoce (de negocio en Información; técnicas de
    /// mensajería o de Booking en Error); <c>ExceptionHandlerMiddleware</c> las repite como un "unhandled exception"
    /// duplicado. Se excluye ESA repetición, solo para las excepciones manejadas, sin ocultar los 500 inesperados.
    /// </summary>
    public static bool IsHandledException(LogEvent logEvent) =>
        logEvent.Exception is BusinessException or MessagePublishException or BookingUnavailableException
        && logEvent.Properties.TryGetValue("SourceContext", out var source)
        && source.ToString().Contains("ExceptionHandlerMiddleware", StringComparison.Ordinal);
}
