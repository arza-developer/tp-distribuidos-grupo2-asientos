using Availability.Application.Specification;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;

namespace Availability.Tests.Infrastructure;

/// <summary>
/// Las MISMAS especificaciones contra SQLite real: garantiza que EF las traduce a SQL (en especial <c>ActiveForSeat</c> con
/// <c>Contains</c> sobre el arreglo de estados) y que el resultado coincide con evaluarlas en memoria.
/// </summary>
public class SpecificationTranslationTests
{
    private static async Task<(SqliteTestDb TestDb, AvailabilityDbContext Db)> InitAsync()
    {
        var testDb = new SqliteTestDb();
        var db = testDb.CreateContext();
        await DbInitializer.InitializeAsync(db);
        return (testDb, db);
    }

    private static SeatRequest NewRequest(Seat seat, ReservationStatus status) => new()
    {
        ReservationId = Guid.NewGuid(), SeatId = seat.SeatId, EventId = seat.EventId, SeatNumber = seat.SeatNumber,
        UserId = Guid.NewGuid(), Price = seat.Price, Status = status, RequestedAtUtc = DateTime.UtcNow, CorrelationId = "c"
    };

    // ---------- SeatSpecification ----------

    [Fact]
    public async Task ByEventAndNumber_IsTranslated_AndIsCaseSensitive()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);

            var found = await repo.FirstOrDefaultAsync(SeatSpecification.ByEventAndNumber(SeedData.EventId, "A-1"));

            Assert.Equal("A-1", found!.SeatNumber);
            Assert.Null(await repo.FirstOrDefaultAsync(SeatSpecification.ByEventAndNumber(SeedData.EventId, "a-1")));
            Assert.Null(await repo.FirstOrDefaultAsync(SeatSpecification.ByEventAndNumber(Guid.NewGuid(), "A-1")));
        }
    }

    [Fact]
    public async Task BySeatId_IsTranslated()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);
            var a3 = SeedData.Seats().Single(s => s.SeatNumber == "A-3");

            var found = await repo.FirstOrDefaultAsync(SeatSpecification.BySeatId(a3.SeatId));

            Assert.Equal("A-3", found!.SeatNumber);
            Assert.Null(await repo.FirstOrDefaultAsync(SeatSpecification.BySeatId(Guid.NewGuid())));
        }
    }

    [Fact]
    public async Task ForEvent_IsTranslated_NullMeansAllAndTheSameSpecServesBothRepositoryMethods()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRepository(db);
            var otherEvent = Guid.NewGuid();
            await repo.AddAsync(new Seat { SeatId = Guid.NewGuid(), EventId = otherEvent, SeatNumber = "Z-1", Price = 1m, IsAvailable = true });
            await repo.SaveChangesAsync();

            Assert.Equal(5, (await repo.ListAsync(SeatSpecification.ForEvent(null))).Count);
            Assert.Equal(4, (await repo.ListAsync(SeatSpecification.ForEvent(SeedData.EventId))).Count);
            Assert.Equal(["Z-1"], (await repo.ListAsync(SeatSpecification.ForEvent(otherEvent))).Select(s => s.SeatNumber));
            Assert.Empty(await repo.ListAsync(SeatSpecification.ForEvent(Guid.NewGuid())));

            // ListByEventAsync reutiliza la misma especificación: mismos resultados.
            Assert.Equal(
                (await repo.ListAsync(SeatSpecification.ForEvent(SeedData.EventId))).Select(s => s.SeatId).Order(),
                (await repo.ListByEventAsync(SeedData.EventId)).Select(s => s.SeatId).Order());
            Assert.Equal(5, (await repo.ListByEventAsync(null)).Count);
        }
    }

    // ---------- SeatRequestSpecification ----------

    [Fact]
    public async Task ByReservationId_IsTranslated()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var seat = SeedData.Seats()[0];
            var request = NewRequest(seat, ReservationStatus.Pending);
            await repo.AddAsync(request);
            await repo.AddAsync(NewRequest(seat, ReservationStatus.Retained));
            await repo.SaveChangesAsync();

            var found = await repo.FirstOrDefaultAsync(SeatRequestSpecification.ByReservationId(request.ReservationId));

            Assert.Equal(request.ReservationId, found!.ReservationId);
            Assert.Null(await repo.FirstOrDefaultAsync(SeatRequestSpecification.ByReservationId(Guid.NewGuid())));
        }
    }

    [Fact]
    public async Task ActiveForSeat_IsTranslated_AndMatchesTheInMemoryEvaluation()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var seat = SeedData.Seats()[0];
            var otherSeat = SeedData.Seats()[1];
            var requests = new[]
            {
                NewRequest(seat, ReservationStatus.Pending),
                NewRequest(seat, ReservationStatus.Retained),
                NewRequest(seat, ReservationStatus.Confirmed),
                NewRequest(seat, ReservationStatus.Rejected),
                NewRequest(seat, ReservationStatus.Expired),
                NewRequest(otherSeat, ReservationStatus.Confirmed)
            };
            foreach (var r in requests) await repo.AddAsync(r);
            await repo.SaveChangesAsync();

            foreach (var excluded in new[] { Guid.Empty, requests[0].ReservationId, requests[2].ReservationId })
            {
                var spec = SeatRequestSpecification.ActiveForSeat(seat.SeatId, excluded);

                var fromSql = (await repo.ListAsync(spec)).Select(r => r.ReservationId).Order().ToList();
                var inMemory = requests.Where(spec.Compile()).Select(r => r.ReservationId).Order().ToList();

                Assert.Equal(inMemory, fromSql);
            }

            // sin exclusiones: Pending + Retained + Confirmed del asiento (no Rejected/Expired ni el otro asiento)
            Assert.Equal(3, (await repo.ListAsync(SeatRequestSpecification.ActiveForSeat(seat.SeatId, Guid.Empty))).Count);
            // excluyendo la Pendiente quedan Retained + Confirmed
            Assert.Equal(2, (await repo.ListAsync(SeatRequestSpecification.ActiveForSeat(seat.SeatId, requests[0].ReservationId))).Count);
        }
    }

    [Fact]
    public async Task ActiveForSeat_WithOnlyFinalRequests_FindsNothing()
    {
        var (testDb, db) = await InitAsync();
        using (testDb) await using (db)
        {
            var repo = new SeatRequestRepository(db);
            var seat = SeedData.Seats()[0];
            await repo.AddAsync(NewRequest(seat, ReservationStatus.Rejected));
            await repo.AddAsync(NewRequest(seat, ReservationStatus.Expired));
            await repo.SaveChangesAsync();

            Assert.Null(await repo.FirstOrDefaultAsync(SeatRequestSpecification.ActiveForSeat(seat.SeatId, Guid.Empty)));
        }
    }
}
