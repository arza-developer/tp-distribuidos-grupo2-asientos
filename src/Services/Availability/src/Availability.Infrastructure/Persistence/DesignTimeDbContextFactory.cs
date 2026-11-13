using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Availability.Infrastructure.Persistence;

/// <summary>Solo para `dotnet ef`: permite generar migraciones sin arrancar el host web.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AvailabilityDbContext>
{
    public AvailabilityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AvailabilityDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
