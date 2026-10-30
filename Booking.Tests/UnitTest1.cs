
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

    [Fact]
    public async Task ConfirmReservation_ShouldBeIdempotent()
    {
        // Creamos una base SQLite temporal.
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

        // Creamos una solicitud de reserva.
        var request = new ReserveSeatRequest
        {
            RequestId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            SeatNumber = "A12",
            UserId = "user-1"
        };

        // Primero retenemos el asiento.
        var retained = await service.TryRetainReservationAsync(request);

        Assert.Equal(ReservationStatus.Retained, retained.Status);

        // Confirmamos por primera vez.
        var confirmed = await service.ConfirmReservationAsync(retained.Id);

        Assert.Equal(ReservationStatus.Confirmed, confirmed.Status);

        // Volvemos a confirmar la misma reserva.
        var confirmedAgain = await service.ConfirmReservationAsync(retained.Id);

        // El resultado debe seguir siendo la misma reserva confirmada.
        Assert.Equal(ReservationStatus.Confirmed, confirmedAgain.Status);
        Assert.Equal(confirmed.Id, confirmedAgain.Id);

        // Verificamos que solamente existe una reserva.
        Assert.Equal(1, await context.Reservations.CountAsync());
    }

    [Fact]
    public async Task RejectReservation_ShouldBeIdempotentForSameReason()
    {
        // Creamos una base SQLite temporal.
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

        // Creamos una reserva retenida.
        var retained = await service.TryRetainReservationAsync(request);

        // Rechazamos la reserva por un pago fallido.
        var rejected = await service.RejectReservationAsync(
            retained.Id,
            ReservationRejectionReason.PaymentFailed);

        Assert.Equal(ReservationStatus.Rejected, rejected.Status);
        Assert.Equal(
            ReservationRejectionReason.PaymentFailed,
            rejected.RejectionReason);
        Assert.Null(rejected.ExpiresAtUtc);

        // Repetimos el rechazo por el mismo motivo.
        var rejectedAgain = await service.RejectReservationAsync(
            retained.Id,
            ReservationRejectionReason.PaymentFailed);

        Assert.Equal(rejected.Id, rejectedAgain.Id);
        Assert.Equal(ReservationStatus.Rejected, rejectedAgain.Status);

        // Un motivo distinto no debe sobrescribir el original.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectReservationAsync(
                retained.Id,
                ReservationRejectionReason.PaymentUnavailable));

        // Verificamos que la base conserva un solo registro.
        Assert.Equal(1, await context.Reservations.CountAsync());
    }


    [Fact]
    public async Task ConfirmedReservation_ShouldNotBeRejected()
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

        var retained = await service.TryRetainReservationAsync(request);

        // Confirmamos la reserva.
        await service.ConfirmReservationAsync(retained.Id);

        // Intentamos rechazar una reserva ya confirmada.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectReservationAsync(
                retained.Id,
                ReservationRejectionReason.PaymentFailed));

        // Comprobamos el estado persistido, no solamente
        // el resultado de la excepción.
        var saved = await context.Reservations
            .AsNoTracking()
            .SingleAsync(r => r.Id == retained.Id);

        Assert.Equal(ReservationStatus.Confirmed, saved.Status);
    }


    [Fact]
    public async Task ExpiredReservation_ShouldChangeStatusToExpired()
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

        var retained = await service.TryRetainReservationAsync(request);

        // Simulamos que la reserva venció hace un minuto.
        var pastDate = DateTime.UtcNow.AddMinutes(-1);

        await context.Reservations
            .Where(r => r.Id == retained.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.ExpiresAtUtc,
                    (DateTime?)pastDate));

        // Ejecutamos el vencimiento.
        var expired = await service.TryExpireReservationAsync(
            retained.Id);

        Assert.Equal(ReservationStatus.Expired, expired.Status);

        // Ejecutarlo nuevamente debe ser seguro.
        var expiredAgain = await service.TryExpireReservationAsync(
            retained.Id);

        Assert.Equal(ReservationStatus.Expired, expiredAgain.Status);
        Assert.Equal(expired.Id, expiredAgain.Id);
    }


    [Fact]
    public async Task ConfirmedReservation_ShouldNotExpire()
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

        var retained = await service.TryRetainReservationAsync(request);

        // Confirmamos antes del vencimiento.
        await service.ConfirmReservationAsync(retained.Id);

        // Intentamos vencer la reserva.
        var result = await service.TryExpireReservationAsync(retained.Id);

        // Debe permanecer confirmada.
        Assert.Equal(ReservationStatus.Confirmed, result.Status);

        var saved = await context.Reservations
            .AsNoTracking()
            .SingleAsync(r => r.Id == retained.Id);

        Assert.Equal(ReservationStatus.Confirmed, saved.Status);
    }


    [Fact]
    public async Task ExpiredDeadline_ShouldPreventConfirmation()
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

        var retained = await service.TryRetainReservationAsync(request);

        // Simulamos que transcurrieron los 10 minutos.
        await context.Reservations
            .Where(r => r.Id == retained.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    r => r.ExpiresAtUtc,
                    (DateTime?)DateTime.UtcNow.AddMinutes(-1)));

        // Una reserva fuera de plazo no puede confirmarse.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConfirmReservationAsync(retained.Id));

        // El Worker realiza finalmente el vencimiento.
        var expired = await service.TryExpireReservationAsync(
            retained.Id);

        Assert.Equal(ReservationStatus.Expired, expired.Status);
    }


    [Fact]
    public async Task ConcurrentConfirmationAndExpiration_ShouldKeepExpired()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"booking-race-{Guid.NewGuid():N}.db");

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
            Guid reservationId;

            // Preparamos una reserva que ya superó su vencimiento.
            await using (var setupContext = new BookingDbContext(options))
            {
                await setupContext.Database.MigrateAsync();

                var service = new BookingService(
                    new BookingRepository(setupContext));

                var retained = await service.TryRetainReservationAsync(
                    new ReserveSeatRequest
                    {
                        RequestId = Guid.NewGuid(),
                        EventId = Guid.NewGuid(),
                        SeatNumber = "A12",
                        UserId = "user-1"
                    });

                reservationId = retained.Id;

                await setupContext.Reservations
                    .Where(r => r.Id == reservationId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            r => r.ExpiresAtUtc,
                            (DateTime?)DateTime.UtcNow.AddMinutes(-1)));
            }

            // Ambas operaciones arrancarán al liberar esta señal.
            var start = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var confirmTask = Task.Run(async () =>
            {
                await start.Task;

                await using var context = new BookingDbContext(options);
                var service = new BookingService(
                    new BookingRepository(context));

                return await service.ConfirmReservationAsync(reservationId);
            });

            var expireTask = Task.Run(async () =>
            {
                await start.Task;

                await using var context = new BookingDbContext(options);
                var service = new BookingService(
                    new BookingRepository(context));

                return await service.TryExpireReservationAsync(reservationId);
            });

            // Habilitamos ambas operaciones.
            start.SetResult(true);

            // La confirmación debe fallar porque el plazo venció.
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => { await confirmTask; });

            // El Worker debe conseguir vencer la reserva.
            var expired = await expireTask;

            Assert.Equal(ReservationStatus.Expired, expired.Status);

            // Verificamos el estado realmente persistido.
            await using var verification = new BookingDbContext(options);

            var saved = await verification.Reservations
                .AsNoTracking()
                .SingleAsync(r => r.Id == reservationId);

            Assert.Equal(ReservationStatus.Expired, saved.Status);
        }
        finally
        {
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
