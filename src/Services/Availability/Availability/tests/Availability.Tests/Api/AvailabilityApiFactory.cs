using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;
using Availability.Tests.Support;
using Availability.Infrastructure.Auth;
using Availability.Infrastructure.Persistence;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Api;

/// <summary>
/// Host de pruebas con una base SQLite temporal propia (eliminada al disponer), el broker reemplazado por un
/// <see cref="RecordingMessagePublisher"/> y Booking reemplazado por un <see cref="RecordingBookingClient"/>.
/// </summary>
public sealed class AvailabilityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"availability-api-{Guid.NewGuid():N}.db");

    public RecordingMessagePublisher Publisher { get; } = new();

    public RecordingBookingClient Booking { get; } = new();

    /// <summary>Reloj controlable (arranca en la hora real) para probar el vencimiento de reservas huérfanas.</summary>
    public FakeTimeProvider Clock { get; } = new();

    /// <summary>Eventos de log capturados (Serilog toma este sink por DI vía ReadFrom.Services).</summary>
    public CapturingSink Logs { get; } = new();

    /// <summary>Auth Server falso para el login de las páginas (password grant).</summary>
    public FakeAuthServerLoginClient LoginClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Availability", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(Publisher);

            services.RemoveAll<IBookingClient>();
            services.AddSingleton<IBookingClient>(Booking);

            services.RemoveAll<IAuthServerLoginClient>();
            services.AddSingleton<IAuthServerLoginClient>(LoginClient);

            services.AddSingleton<Serilog.Core.ILogEventSink>(Logs);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>
    /// Cliente HTTP con token Bearer: por defecto el de un usuario (rol Client, sub = <see cref="TestJwt.ClientUserId"/>).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string? token = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? TestJwt.ForClient());
        return client;
    }

    /// <summary>Cliente de navegador: guarda cookies y NO sigue redirecciones (los tests inspeccionan cada respuesta).</summary>
    public HttpClient CreateBrowserClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>
    /// Inserta un asiento propio y disponible (número único) para que cada test sea independiente aunque la
    /// fábrica sea compartida: una reserva exitosa deja el asiento no disponible.
    /// </summary>
    public async Task<string> CreateAvailableSeatAsync()
    {
        var number = $"T-{Guid.NewGuid():N}"[..12];
        using var scope = Services.CreateScope();
        var seats = scope.ServiceProvider.GetRequiredService<ISeatRepository>();
        await seats.AddAsync(new Seat
        {
            SeatId = Guid.NewGuid(), EventId = SeedData.EventId, SeatNumber = number, Price = 15000m, IsAvailable = true
        });
        await seats.SaveChangesAsync();
        return number;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Solo el pool de ESTA base: ClearAllPools vaciaría también las de otros tests que corren en paralelo.
        using (var own = new SqliteConnection($"Data Source={_dbPath}")) SqliteConnection.ClearPool(own);
        foreach (var f in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
