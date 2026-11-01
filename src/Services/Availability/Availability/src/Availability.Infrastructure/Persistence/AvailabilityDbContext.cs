using Availability.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Availability.Infrastructure.Persistence;

public class AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options) : DbContext(options)
{
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatRequest> SeatRequests => Set<SeatRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AvailabilityDbContext).Assembly);
}
