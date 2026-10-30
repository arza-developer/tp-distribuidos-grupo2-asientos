
using System.IO;
using System.Linq;
using Booking.Api.Data;
using Booking.Api.DTOs;
using Booking.Api.Models.Enums;
using Booking.Api.Repositories;
using Booking.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Booking.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task FreeSeat_ShouldCreateRetainedReservation()
    {
        // Creamos una base SQLite temporal en memoria.
        using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new BookingDbContext(options);

        // Aplicamos las migraciones sobre la base de prueba.
        await context.Database.MigrateAsync();

        // Creamos las dependencias del servicio.
        var repository = new BookingRepository(context);
        var service = new BookingService(repository);

        // Preparamos una solicitud de reserva.
        var request = new ReserveSeatRequest
        {
            RequestId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            SeatNumber = "A12",
            UserId = "user-1"
        };

        // Ejecutamos la operación que queremos probar.
        var result = await service.TryRetainReservationAsync(request);

        // Verificamos el resultado esperado.
        Assert.Equal(ReservationStatus.Retained, result.Status);
        Assert.Equal(request.RequestId, result.RequestId);
    }

    [Fact]
    public async Task OccupiedSeat_ShouldCreateRejectedReservation()
    {
        // Base SQLite temporal para esta prueba.
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new BookingDbContext(options);
        await context.Database.MigrateAsync();

        var repository = new BookingRepository(context);
        var service = new BookingService(repository);

        var eventId = Guid.NewGuid();

        // Primera solicitud: debería retener el asiento.
        var firstRequest = new ReserveSeatRequest
        {
            RequestId = Guid.NewGuid(),
            EventId = eventId,
            SeatNumber = "A12",
            UserId = "user-1"
        };

        // Segunda solicitud: mismo asiento, distinto RequestId.
        var secondRequest = new ReserveSeatRequest
        {
            RequestId = Guid.NewGuid(),
            EventId = eventId,
            SeatNumber = "A12",
            UserId = "user-2"
        };

        var first = await service.TryRetainReservationAsync(firstRequest);
        var second = await service.TryRetainReservationAsync(secondRequest);

        Assert.Equal(ReservationStatus.Retained, first.Status);
        Assert.Equal(ReservationStatus.Rejected, second.Status);

        Assert.Equal(
            ReservationRejectionReason.SeatOccupied,
            second.RejectionReason);

        Assert.Null(second.ExpiresAtUtc);

        // Ambas solicitudes deben conservarse en el historial.
        Assert.Equal(2, await context.Reservations.CountAsync());
    }

    [Fact]
    public async Task DuplicateRequest_ShouldReturnOriginalReservation()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new BookingDbContext(options);
        await context.Database.MigrateAsync();

        var repository = new BookingRepository(context);
        var service = new BookingService(repository);

        var request = new ReserveSeatRequest
        {
            RequestId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            SeatNumber = "A12",
            UserId = "user-1"
        };

        // Procesamos la misma solicitud dos veces.
        var first = await service.TryRetainReservationAsync(request);
        var second = await service.TryRetainReservationAsync(request);

        // Debemos recuperar la misma reserva.
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.RequestId, second.RequestId);
        Assert.Equal(ReservationStatus.Retained, second.Status);

        // No debe existir una segunda reserva.
        Assert.Equal(1, await context.Reservations.CountAsync());
    }

    [Fact]
    public async Task TenConcurrentRequests_ShouldRetainOnlyOneSeat()
    {
        // Usamos un archivo SQLite temporal para permitir
        // conexiones independientes a la misma base de datos.
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"booking-test-{Guid.NewGuid():N}.db");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            // Creamos la estructura antes de iniciar la concurrencia.
            await using (var setupContext = new BookingDbContext(options))
            {
                await setupContext.Database.MigrateAsync();
            }

            var eventId = Guid.NewGuid();

            var requests = Enumerable.Range(0, 10)
                .Select(i => new ReserveSeatRequest
                {
                    RequestId = Guid.NewGuid(),
                    EventId = eventId,
                    SeatNumber = "A12",
                    UserId = $"user-{i}"
                })
                .ToArray();

            // Permite iniciar las diez operaciones juntas.
            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = requests.Select(async request =>
            {
                await start.Task;

                // Cada operación utiliza su propio DbContext.
                await using var context = new BookingDbContext(options);

                var repository = new BookingRepository(context);
                var service = new BookingService(repository);

                return await service.TryRetainReservationAsync(request);
            }).ToArray();

            start.SetResult(true);

            // Esperamos el resultado de las diez solicitudes.
            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(r =>
                r.Status == ReservationStatus.Retained));

            Assert.Equal(9, results.Count(r =>
                r.Status == ReservationStatus.Rejected));

            // Verificamos también lo realmente guardado en SQLite.
            await using var verification = new BookingDbContext(options);

            var saved = await verification.Reservations
                .AsNoTracking()
                .Where(r => r.EventId == eventId &&
                            r.SeatNumber == "A12")
                .ToListAsync();

            Assert.Equal(10, saved.Count);

            Assert.Single(saved.Where(r =>
                r.Status == ReservationStatus.Retained));
        }
        finally
        {
            // Eliminamos los archivos temporales de SQLite.
            foreach (var file in new[]
            {
            dbPath,
            dbPath + "-wal",
            dbPath + "-shm"
        })
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }
}
