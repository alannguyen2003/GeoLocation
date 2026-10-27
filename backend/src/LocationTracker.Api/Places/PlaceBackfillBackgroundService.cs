using LocationTracker.Business.Services;

namespace LocationTracker.Api.Places;

/// <summary>
/// Once per start: gives city, region and country to points stored before place names existed.
/// New points get them on upload, so this has nothing to do after the first run.
/// </summary>
public class PlaceBackfillBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PlaceBackfillBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, timeProvider, stoppingToken);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ILocationService>().FillMissingPlacesAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never take the host down over this; the next start tries again.
            logger.LogWarning(ex, "Place name backfill failed");
        }
    }
}
