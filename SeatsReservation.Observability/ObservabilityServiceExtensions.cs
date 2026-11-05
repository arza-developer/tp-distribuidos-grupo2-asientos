using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SeatsReservation.Observability;

public static class ObservabilityServiceExtensions
{
    public static IServiceCollection AddSeatsObservability(this IServiceCollection services)
    {
        // Registrar interceptores gRPC o servicios adicionales si es necesario
        services.AddTransient<GrpcCorrelationInterceptor>();
        return services;
    }

    public static IApplicationBuilder UseSeatsObservability(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}