using Availability.Application.Specification;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Infrastructure.Persistence.Repository;

namespace Availability.Tests.Infrastructure;

/// <summary>
/// Traducción a SQL (SQLite real) de la especificación no trivial <c>ActiveForSeat</c> (TD-11: el resto de las specs lo ejercen los tests de handlers y de contrato). Antes: las MISMAS especificaciones contra SQLite real: garantiza que EF las traduce a SQL (en especial <c>ActiveForSeat</c> con
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
}
