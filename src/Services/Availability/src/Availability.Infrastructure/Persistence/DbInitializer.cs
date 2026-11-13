using Microsoft.EntityFrameworkCore;

namespace Availability.Infrastructure.Persistence;

/// <summary>
/// Punto único de arranque de la base: aplica migraciones y ejecuta los seeders.
/// Cada entidad con datos iniciales suma su propio método SeedXxxAsync, idempotente.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AvailabilityDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedSeatsAsync(db, ct);
    }

    private static async Task SeedSeatsAsync(AvailabilityDbContext db, CancellationToken ct)
    {
        if (await db.Seats.AnyAsync(ct)) return;

        db.Seats.AddRange(SeedData.Seats());
        await db.SaveChangesAsync(ct);
    }
}
