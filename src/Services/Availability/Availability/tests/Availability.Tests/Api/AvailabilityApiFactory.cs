using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;
using Availability.Infrastructure.Persistence;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Availability", $"Data Source={_dbPath}");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(Publisher);

            services.RemoveAll<IBookingClient>();
            services.AddSingleton<IBookingClient>(Booking);
        });
    }

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

        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
