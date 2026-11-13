using Availability.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Availability.Tests.Infrastructure;

public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsync_SeedsFourSeats()
    {
        using var testDb = new SqliteTestDb();
        await using var db = testDb.CreateContext();

        await DbInitializer.InitializeAsync(db);

        var seats = await db.Seats.OrderBy(s => s.SeatNumber).ToListAsync();
        Assert.Equal(["A-1", "A-2", "A-3", "B-1"], seats.Select(s => s.SeatNumber));
        Assert.False(seats.Single(s => s.SeatNumber == "A-3").IsAvailable);
        Assert.All(seats, s => Assert.Equal(SeedData.EventId, s.EventId));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        using var testDb = new SqliteTestDb();
        await using var db = testDb.CreateContext();

        await DbInitializer.InitializeAsync(db);
        await DbInitializer.InitializeAsync(db);

        var seats = await db.Seats.ToListAsync();
        Assert.Equal(4, seats.Count);
        Assert.Equal(4, seats.Select(s => s.SeatId).Distinct().Count());
    }
}
