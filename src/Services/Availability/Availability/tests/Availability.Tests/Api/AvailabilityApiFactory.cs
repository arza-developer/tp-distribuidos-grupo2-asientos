using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Availability.Tests.Api;

/// <summary>Host de pruebas con una base SQLite temporal propia, eliminada al disponer.</summary>
public sealed class AvailabilityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"availability-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Availability", $"Data Source={_dbPath}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
