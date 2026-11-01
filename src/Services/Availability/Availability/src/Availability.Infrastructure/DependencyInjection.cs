using Availability.Application.Abstractions;
using Availability.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // La cadena se resuelve de forma diferida desde IConfiguration (permite overrides en tests/entornos).
        services.AddDbContext<AvailabilityDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Availability")
                                   ?? "Data Source=availability.db";
            options.UseSqlite(connectionString);
        });

        // Un par IXRepository/XRepository por entidad (la base genérica no se registra).
        services.AddScoped<ISeatRepository, SeatRepository>();
        return services;
    }
}
