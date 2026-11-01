using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;

namespace Availability.Tests.Infrastructure;

/// <summary>
/// Evidencia de por qué <c>Update</c> y <c>Remove</c> del repositorio son SÍNCRONOS: EF Core solo cambia el estado
/// en el ChangeTracker; nada se escribe en la base hasta <c>SaveChangesAsync</c>.
/// Se usan dos contextos sobre la misma base para observar qué ve "otro lector".
/// </summary>
public class UpdateRemoveTrackingTests
{
    [Fact]
    public async Task Update_OnlyMarksTrackerState_UntilSaveChangesAsync()
    {
        using var testDb = new SqliteTestDb();
        await using var writer = testDb.CreateContext();
        await DbInitializer.InitializeAsync(writer);
        var repo = new SeatRepository(writer);
        var seat = (await repo.FirstOrDefaultAsync(s => s.SeatNumber == "A-1"))!;
        var tracked = (await repo.GetByIdAsync(seat.Id))!;
        tracked.IsAvailable = false;

        repo.Update(tracked);

        Assert.Equal(EntityState.Modified, writer.Entry(tracked).State);
        await using (var reader = testDb.CreateContext())
            Assert.True((await reader.Seats.SingleAsync(s => s.Id == seat.Id)).IsAvailable); // la base no cambió

        await repo.SaveChangesAsync();

        await using (var reader = testDb.CreateContext())
            Assert.False((await reader.Seats.SingleAsync(s => s.Id == seat.Id)).IsAvailable);
    }

    [Fact]
    public async Task Remove_OnlyMarksTrackerState_UntilSaveChangesAsync()
    {
        using var testDb = new SqliteTestDb();
        await using var writer = testDb.CreateContext();
        await DbInitializer.InitializeAsync(writer);
        var repo = new SeatRequestRepository(writer);
        var request = new SeatRequest
        {
            ReservationId = Guid.NewGuid(), SeatId = Guid.NewGuid(), EventId = SeedData.EventId, SeatNumber = "A-1",
            UserId = Guid.NewGuid(), Price = 1m, Status = ReservationStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow, CorrelationId = "c"
        };
        await repo.AddAsync(request);
        await repo.SaveChangesAsync();

        repo.Remove(request);

        Assert.Equal(EntityState.Deleted, writer.Entry(request).State);
        await using (var reader = testDb.CreateContext())
            Assert.Equal(1, await reader.SeatRequests.CountAsync()); // la fila sigue en la base

        await repo.SaveChangesAsync();

        await using (var reader = testDb.CreateContext())
            Assert.Equal(0, await reader.SeatRequests.CountAsync());
    }
}
