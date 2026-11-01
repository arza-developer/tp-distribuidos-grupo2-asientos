using Availability.Application.Seats.Queries.GetSeats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Availability.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetSeatsQuery>());

        // Reloj único (inyectable y controlable en tests).
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
