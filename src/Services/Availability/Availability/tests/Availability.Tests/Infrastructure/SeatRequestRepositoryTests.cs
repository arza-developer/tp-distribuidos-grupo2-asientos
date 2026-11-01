using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Availability.Tests.Infrastructure;

public class SeatRequestRepositoryTests
{
    private static SeatRequest NewRequest(Guid? reservationId = null) => new()
    {
        ReservationId = reservationId ?? Guid.NewGuid(),
        SeatId = SeedData.Seats()[0].SeatId,
        EventId = SeedData.EventId,
        SeatNumber = "A-1",
        UserId = Guid.NewGuid(),
        Price = 15000m,
        Status = ReservationStatus.Pending,
        RequestedAtUtc = DateTime.UtcNow,
        CorrelationId = "corr-1"
    };

    private static async Task<(SqliteTestDb TestDb, AvailabilityDbContext Db)> InitAsync()
    {
        var testDb = new SqliteTestDb();
        var db = testDb.CreateContext();
        await DbInitializer.InitializeAsync(db);
        return (testDb, db);
    }

    [Fact]
    public async Task AddAsync_PersistsWithGeneratedIdAndStatus()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var request = NewRequest();

            await repo.AddAsync(request);
            await repo.SaveChangesAsync();

            Assert.True(request.Id > 0);
            var stored = await repo.GetByIdAsync(request.Id);
            Assert.Equal(ReservationStatus.Pending, stored!.Status);
            Assert.Equal(15000m, stored.Price);
        }
    }

    [Fact]
    public async Task FirstOrDefaultAsync_FindsByExternalReservationId()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var reservationId = Guid.NewGuid();
            await repo.AddAsync(NewRequest(reservationId));
            await repo.AddAsync(NewRequest());
            await repo.SaveChangesAsync();

            var found = await repo.FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            Assert.NotNull(found);
            Assert.Equal(reservationId, found!.ReservationId);
            var unknown = Guid.NewGuid();
            Assert.Null(await repo.FirstOrDefaultAsync(r => r.ReservationId == unknown));
        }
    }

    [Fact]
    public async Task SaveChanges_DuplicateReservationId_Throws()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var reservationId = Guid.NewGuid();
            await repo.AddAsync(NewRequest(reservationId));
            await repo.AddAsync(NewRequest(reservationId));

            await Assert.ThrowsAsync<DbUpdateException>(() => repo.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Remove_DeletesTheRequest()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var request = NewRequest();
            await repo.AddAsync(request);
            await repo.SaveChangesAsync();

            repo.Remove(request);
            await repo.SaveChangesAsync();

            Assert.Empty(await repo.ListAsync());
        }
    }
}
