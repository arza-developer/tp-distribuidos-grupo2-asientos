using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Infrastructure.Auth;
using Availability.Infrastructure.Bookings;
using Availability.Infrastructure.Messaging;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SeatsReservation.Contracts.Events;

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
        services.AddOptions<AzureQueueOptions>().BindConfiguration(AzureQueueOptions.SectionName);
        services.AddSingleton<IMessagePublisher, AzureQueuePublisher>();

        services.TryAddSingleton(TimeProvider.System); // el proveedor de token calcula vencimientos con el reloj inyectable

        // Auth Server: token M2M (Client Credentials) para llamar a Booking, con cache compartido (singleton).
        services.AddOptions<AuthServerOptions>().BindConfiguration(AuthServerOptions.SectionName);
        services.AddHttpClient(AuthServerOptions.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AuthServerOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
        services.AddSingleton<IAuthServerLoginClient, AuthServerLoginClient>(); // login de usuarios para las páginas MVC (TD-9)
        services.AddTransient<BookingAuthHandler>();

        // Booking (solo lectura): HttpClient tipado con BaseUrl y timeout corto desde la configuración,
        // autenticado con el token M2M (req. 3.e entre servicios).
        services.AddOptions<BookingOptions>().BindConfiguration(BookingOptions.SectionName);
        services.AddHttpClient<IBookingClient, BookingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<BookingOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).AddHttpMessageHandler<BookingAuthHandler>();

        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<ISeatRequestRepository, SeatRequestRepository>();
        return services;
    }
}
