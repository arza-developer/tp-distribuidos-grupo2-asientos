using Availability.Application.Seats.Queries.GetSeats;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetSeatsQuery>());
        return services;
    }
}
