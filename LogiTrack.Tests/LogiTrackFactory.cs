using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Tests;

// Runs the real app against a throwaway SQLite file built from the real migrations
public class LogiTrackFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Test-Admin-Password-1!";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"logitrack-test-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    public LogiTrackFactory()
    {
        // The app seeds roles on startup, so the schema has to exist before the host starts
        using var context = new LogiTrackContext(
            new DbContextOptionsBuilder<LogiTrackContext>().UseSqlite(ConnectionString).Options);
        context.Database.Migrate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LogiTrack", ConnectionString);
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-at-least-32-bytes-long");
        builder.UseSetting("SeedAdmin:UserName", AdminUserName);
        builder.UseSetting("SeedAdmin:Email", "admin@test.local");
        builder.UseSetting("SeedAdmin:Password", AdminPassword);
        // All test requests share one client IP, so lift the login/register limit
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
