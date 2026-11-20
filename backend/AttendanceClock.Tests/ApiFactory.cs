using AttendanceClock.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AttendanceClock.Tests;

/// <summary>
/// Runs the real API in memory, against the real SQL Server but a SEPARATE database
/// (AttendanceClock_Tests), so tests never touch development data.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestDatabaseName = "AttendanceClock_Tests";

    /// <summary>Same server as the app (from docker-compose), different database name.</summary>
    public static string TestConnectionString { get; } = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Server=localhost,1433;User Id=sa;Password=Dev_Passw0rd_2026!;TrustServerCertificate=True")
    {
        InitialCatalog = TestDatabaseName,
    }.ConnectionString;

    /// <summary>High by default so ordinary tests don't trip the brute-force limit.</summary>
    public int LoginAttemptsPerMinute { get; init; } = 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // runs migrations + demo-account seeding on startup
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = TestConnectionString,
            ["Auth:LoginAttemptsPerMinute"] = LoginAttemptsPerMinute.ToString(),
        }));
    }

    /// <summary>Deletes the test database, so each test run starts clean (the app recreates it on startup).</summary>
    public static void ResetDatabase()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestConnectionString).Options);
        db.Database.EnsureDeleted();
    }
}
