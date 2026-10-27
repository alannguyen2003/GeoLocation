using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LocationTracker.Data;

/// <summary>
/// Used only by the dotnet-ef tools so migrations can be added without a configured database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=location_tracker;Username=postgres;Password=dev")
            .Options;

        return new AppDbContext(options);
    }
}
