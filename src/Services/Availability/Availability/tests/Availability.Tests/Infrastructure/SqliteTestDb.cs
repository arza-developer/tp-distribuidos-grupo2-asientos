using Availability.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Availability.Tests.Infrastructure;

/// <summary>Base SQLite en archivo temporal, eliminada al disponer.</summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"availability-test-{Guid.NewGuid():N}.db");

    public AvailabilityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AvailabilityDbContext>().UseSqlite($"Data Source={_path}").Options);

    public void Dispose()
    {
        // Solo el pool de ESTA base: ClearAllPools vaciaría también las de otros tests que corren en paralelo.
        using (var own = new SqliteConnection($"Data Source={_path}")) SqliteConnection.ClearPool(own);
        foreach (var f in new[] { _path, _path + "-shm", _path + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
