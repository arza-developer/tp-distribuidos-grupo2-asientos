using Availability.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Availability.FunctionalTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
  // ":memory:" is not a valid connection string for Microsoft.Data.Sqlite and a pooled
  // in-memory database would be lost between connections, so use a temp file per factory.
  private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"availability-tests-{Guid.NewGuid():N}.db");

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseSetting("ConnectionStrings:SqliteConnection", $"Data Source={_dbPath}");
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing)
    {
      SqliteConnection.ClearAllPools();
      try { File.Delete(_dbPath); } catch (IOException) { }
    }
  }

  /// <summary>
  /// Overriding CreateHost to avoid creating a separate ServiceProvider per this thread:
  /// https://github.com/dotnet-architecture/eShopOnWeb/issues/465
  /// </summary>
  /// <param name="builder"></param>
  /// <returns></returns>
  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing"); // will not send real emails
    var host = builder.Build();
    host.Start();

    // Create a scope to obtain a reference to the database context (AppDbContext).
    using (var scope = host.Services.CreateScope())
    {
      var scopedServices = scope.ServiceProvider;
      var db = scopedServices.GetRequiredService<AppDbContext>();

      var logger = scopedServices
          .GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();

      try
      {
        db.Database.EnsureCreated();

        // Seed the database with test data only if it has not been seeded yet.
        SeedData.InitializeAsync(db).Wait();
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred seeding the " +
                            "database with test messages. Error: {exceptionMessage}", ex.Message);
      }
    }

    return host;
  }
}
