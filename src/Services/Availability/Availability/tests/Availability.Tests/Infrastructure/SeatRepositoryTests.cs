using Availability.Domain.Entities;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;

namespace Availability.Tests.Infrastructure;

public class SeatRepositoryTests
{
    private static async Task<(SqliteTestDb TestDb, AvailabilityDbContext Db)> InitAsync()
    {
        var testDb = new SqliteTestDb();
        var db = testDb.CreateContext();
        await DbInitializer.InitializeAsync(db);
        return (testDb, db);
    }

    [Fact]
    public async Task ListAsync_WithPredicate_FiltersSeats()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);

            var free = await repo.ListAsync(s => s.IsAvailable);

            Assert.Equal(3, free.Count);
            Assert.Equal(4, (await repo.ListAsync()).Count);
        }
    }

    [Fact]
    public async Task FirstOrDefaultAsync_FindsBySeatId_AndGetByIdUsesInternalId()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);
            var seatId = SeedData.Seats()[2].SeatId;

            var seat = await repo.FirstOrDefaultAsync(s => s.SeatId == seatId);

            Assert.NotNull(seat);
            Assert.Equal("A-3", seat!.SeatNumber);
            Assert.Equal("A-3", (await repo.GetByIdAsync(seat.Id))!.SeatNumber);
        }
    }

    [Fact]
    public async Task AddAsync_And_SaveChanges_PersistsWithGeneratedId()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);
            var seat = new Seat { SeatId = Guid.NewGuid(), EventId = SeedData.EventId, SeatNumber = "C-1", Price = 10m, IsAvailable = true };

            await repo.AddAsync(seat);
            await repo.SaveChangesAsync();

            Assert.True(seat.Id > 0);
            Assert.Equal(5, (await repo.ListAsync()).Count);
        }
    }

    [Fact]
    public async Task ListByEventAsync_FiltersByEvent_AndNullReturnsAll()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);

            Assert.Equal(4, (await repo.ListByEventAsync(SeedData.EventId)).Count);
            Assert.Empty(await repo.ListByEventAsync(Guid.NewGuid()));
            Assert.Equal(4, (await repo.ListByEventAsync(null)).Count);
        }
    }
}
