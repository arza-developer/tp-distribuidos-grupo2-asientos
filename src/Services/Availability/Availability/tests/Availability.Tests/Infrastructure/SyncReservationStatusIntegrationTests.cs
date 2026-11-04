using Availability.Application.Bookings;
using Availability.Application.Reservations;
using Availability.Application.Reservations.Commands.SyncReservationStatus;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;
using Availability.Tests.Api;
using Availability.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Availability.Tests.Infrastructure;

/// <summary>
/// El handler de sincronización con repositorios reales sobre SQLite: la solicitud y el asiento se persisten
/// juntos (un único SaveChanges) y se leen desde otro contexto. Se siembra con un contexto propio y el handler
/// corre en uno nuevo, como en una request real (scope con DbContext sin entidades previamente trackeadas).
/// </summary>
public class SyncReservationStatusIntegrationTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    /// <summary>Estado posterior a un POST exitoso: asiento A-1 bloqueado y una solicitud Pending. Devuelve ids.</summary>
    private static async Task<(Guid ReservationId, Guid SeatId, Guid EventId)> SeedReservedSeatA1Async(SqliteTestDb testDb)
    {
        await using var seed = testDb.CreateContext();
        await DbInitializer.InitializeAsync(seed);

        var seat = await seed.Seats.SingleAsync(s => s.SeatNumber == "A-1");
        seat.IsAvailable = false;
        var request = new SeatRequest
        {
            ReservationId = Guid.NewGuid(), SeatId = seat.SeatId, EventId = seat.EventId, SeatNumber = seat.SeatNumber,
            UserId = OwnerId, Price = seat.Price, Status = ReservationStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow, CorrelationId = "corr"
        };
        seed.SeatRequests.Add(request);
        await seed.SaveChangesAsync();
        return (request.ReservationId, seat.SeatId, seat.EventId);
    }

    private static async Task SeedOtherRequestAsync(SqliteTestDb testDb, Guid seatId, Guid eventId, ReservationStatus status)
    {
        await using var seed = testDb.CreateContext();
        seed.SeatRequests.Add(new SeatRequest
        {
            ReservationId = Guid.NewGuid(), SeatId = seatId, EventId = eventId, SeatNumber = "A-1",
            UserId = Guid.NewGuid(), Price = 1m, Status = status, RequestedAtUtc = DateTime.UtcNow, CorrelationId = "otra"
        });
        await seed.SaveChangesAsync();
    }

    private static SyncReservationStatusCommandHandler CreateHandler(
        AvailabilityDbContext db, RecordingBookingClient booking, TimeProvider? clock = null) =>
        new(new SeatRequestRepository(db), new SeatRepository(db), booking, new FakeCurrentUserAccessor(OwnerId), clock ?? TimeProvider.System,
            Options.Create(new ReservationSyncOptions()), NullLogger<SyncReservationStatusCommandHandler>.Instance);

    [Fact]
    public async Task Sync_Rejected_PersistsRequestStatusAndSeatReleaseTogether()
    {
        using var testDb = new SqliteTestDb();
        var (reservationId, seatId, _) = await SeedReservedSeatA1Async(testDb);
        var booking = new RecordingBookingClient { Result = new BookingStatusInfo(ReservationStatus.Rejected, null, "A-1") };

        await using (var db = testDb.CreateContext())
            await CreateHandler(db, booking).Handle(new SyncReservationStatusCommand(reservationId), default);

        await using var reader = testDb.CreateContext();
        Assert.Equal(ReservationStatus.Rejected, (await reader.SeatRequests.SingleAsync()).Status);
        Assert.True((await reader.Seats.SingleAsync(s => s.SeatId == seatId)).IsAvailable);
    }

    [Fact]
    public async Task Sync_Rejected_WithAnotherActiveRequest_PersistsStatusButKeepsSeatBlocked()
    {
        using var testDb = new SqliteTestDb();
        var (reservationId, seatId, eventId) = await SeedReservedSeatA1Async(testDb);
        await SeedOtherRequestAsync(testDb, seatId, eventId, ReservationStatus.Confirmed);
        var booking = new RecordingBookingClient { Result = new BookingStatusInfo(ReservationStatus.Rejected, null, "A-1") };

        await using (var db = testDb.CreateContext())
            await CreateHandler(db, booking).Handle(new SyncReservationStatusCommand(reservationId), default);

        await using var reader = testDb.CreateContext();
        Assert.Equal(ReservationStatus.Rejected, (await reader.SeatRequests.SingleAsync(r => r.ReservationId == reservationId)).Status);
        Assert.False((await reader.Seats.SingleAsync(s => s.SeatId == seatId)).IsAvailable);
    }

    [Fact]
    public async Task Sync_OrphanPending_PersistsExpiredAndReleasesSeat()
    {
        using var testDb = new SqliteTestDb();
        var (reservationId, seatId, _) = await SeedReservedSeatA1Async(testDb);
        var clock = new FakeTimeProvider();
        clock.Advance(TimeSpan.FromMinutes(11));
        var booking = new RecordingBookingClient { Result = null }; // Booking nunca la conoció

        await using (var db = testDb.CreateContext())
            await CreateHandler(db, booking, clock).Handle(new SyncReservationStatusCommand(reservationId), default);

        await using var reader = testDb.CreateContext();
        Assert.Equal(ReservationStatus.Expired, (await reader.SeatRequests.SingleAsync()).Status);
        Assert.True((await reader.Seats.SingleAsync(s => s.SeatId == seatId)).IsAvailable);
    }

    [Fact]
    public async Task Sync_BookingDown_LeavesDatabaseUntouched()
    {
        using var testDb = new SqliteTestDb();
        var (reservationId, seatId, _) = await SeedReservedSeatA1Async(testDb);
        var clock = new FakeTimeProvider();
        clock.Advance(TimeSpan.FromMinutes(60));
        var booking = new RecordingBookingClient { Throw = new Availability.Application.Exceptions.BookingUnavailableException("caído") };

        await using (var db = testDb.CreateContext())
            await Assert.ThrowsAsync<Availability.Application.Exceptions.BookingUnavailableException>(
                () => CreateHandler(db, booking, clock).Handle(new SyncReservationStatusCommand(reservationId), default));

        await using var reader = testDb.CreateContext();
        Assert.Equal(ReservationStatus.Pending, (await reader.SeatRequests.SingleAsync()).Status);
        Assert.False((await reader.Seats.SingleAsync(s => s.SeatId == seatId)).IsAvailable);
    }
}
