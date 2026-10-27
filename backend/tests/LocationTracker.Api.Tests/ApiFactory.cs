using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace LocationTracker.Api.Tests;

/// <summary>
/// Runs the real API against a throwaway Postgres container. When Docker is not available,
/// <see cref="SkipReason"/> is set and every test skips itself instead of failing.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AllowedOrigin = "https://tracker.example.com";

    private PostgreSqlContainer? _postgres;

    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:17").Build();
            await _postgres.StartAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Docker is not available, skipping integration tests: {ex.Message}";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DATABASE_URL", string.Empty);
        builder.UseSetting("ConnectionStrings:Default", _postgres!.GetConnectionString());
        builder.UseSetting("Cors:AllowedOrigins", $"{AllowedOrigin},http://localhost:8081");
        // Every test registers its own device from the same (test server) address.
        builder.UseSetting("RateLimiting:RegistrationsPerHourPerIp", "1000");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
