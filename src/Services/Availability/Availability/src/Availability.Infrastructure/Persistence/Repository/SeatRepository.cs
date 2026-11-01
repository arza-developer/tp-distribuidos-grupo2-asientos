using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Availability.Infrastructure.Persistence.Repository;

public class SeatRepository(AvailabilityDbContext db) : GenericRepository<Seat>(db), ISeatRepository
{
    public async Task<IReadOnlyList<Seat>> ListByEventAsync(Guid? eventId, CancellationToken ct = default) =>
        await Db.Seats
            .AsNoTracking()
            .Where(s => eventId == null || s.EventId == eventId)
            .ToListAsync(ct);
}
