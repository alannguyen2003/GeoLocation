using LocationTracker.Business.Services;
using Microsoft.Extensions.Options;

namespace LocationTracker.Api.Retention;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    public int Days { get; set; } = 90;
}

/// <summary>Deletes points older than Retention:Days once a day.</summary>
public class RetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionBackgroundService> logger) : BackgroundService
{
    // The free Render instance sleeps when idle and rarely stays up for a full day,
    // so also run shortly after every start instead of only on the daily tick.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, timeProvider, stoppingToken);

        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var locations = scope.ServiceProvider.GetRequiredService<ILocationService>();
            await locations.DeleteExpiredAsync(options.Value.Days, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never take the host down over a failed cleanup; the next run will try again.
            logger.LogWarning(ex, "Retention cleanup failed");
        }
    }
}
