using LocationTracker.Business.Models;
using LocationTracker.Business.Validation;
using LocationTracker.Data.Places;
using LocationTracker.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace LocationTracker.Business.Services;

public class LocationService(
    ILocationRepository locations,
    IPlaceLookup places,
    TimeProvider timeProvider,
    ILogger<LocationService> logger) : ILocationService
{
    private const int BackfillPageSize = 500;

    public async Task<BatchUploadResult> UploadBatchAsync(
        Guid deviceId,
        IReadOnlyList<NewLocationPoint>? points,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        LocationRules.ValidateBatch(points, now);

        // Duplicates inside the batch are dropped here; duplicates of points already stored
        // are dropped by the repository's unique (device_id, client_point_id) insert.
        var unique = points!
            .DistinctBy(p => p.ClientPointId)
            .Select(p => p.ToEntity(deviceId, now, FindPlace(p.Latitude, p.Longitude)))
            .ToList();

        var accepted = await locations.InsertIgnoringDuplicatesAsync(unique, cancellationToken);
        var result = new BatchUploadResult(accepted, points!.Count - accepted);

        logger.LogDebug(
            "Device {DeviceId} uploaded {Total} points: {Accepted} accepted, {Duplicates} duplicates",
            deviceId, points.Count, result.Accepted, result.Duplicates);

        return result;
    }

    public async Task<IReadOnlyList<LocationPointModel>> GetPointsAsync(
        Guid deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = LocationRules.ValidateQuery(from, to, limit);
        var points = await locations.GetForDeviceAsync(
            deviceId,
            from?.ToUniversalTime(),
            to?.ToUniversalTime(),
            effectiveLimit,
            cancellationToken);

        return points.Select(p => p.ToModel()).ToList();
    }

    public async Task<LocationPointModel?> GetLatestAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var point = await locations.GetLatestAsync(deviceId, cancellationToken);
        return point?.ToModel();
    }

    public async Task<int> DeleteExpiredAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        if (retentionDays < 1)
        {
            throw new BusinessValidationException("retentionDays", "Retention must be at least 1 day.");
        }

        var cutoff = timeProvider.GetUtcNow() - TimeSpan.FromDays(retentionDays);
        var deleted = await locations.DeleteRecordedBeforeAsync(cutoff, cancellationToken);
        logger.LogInformation("Retention: deleted {Count} points recorded before {Cutoff:O}", deleted, cutoff);

        return deleted;
    }

    public async Task<int> FillMissingPlacesAsync(CancellationToken cancellationToken = default)
    {
        var filled = 0;
        var afterId = 0L;
        while (true)
        {
            var page = await locations.GetWithoutPlaceAsync(afterId, BackfillPageSize, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var point in page)
            {
                if (FindPlace(point.Latitude, point.Longitude) is { } place)
                {
                    await locations.SetPlaceAsync(point.Id, place, cancellationToken);
                    filled++;
                }
            }

            afterId = page[^1].Id;
        }

        if (filled > 0)
        {
            logger.LogInformation("Filled place names for {Count} points", filled);
        }

        return filled;
    }

    private PlaceMatch? FindPlace(double latitude, double longitude) =>
        places.FindNearest(latitude, longitude, LocationRules.MaxPlaceDistanceKm);
}
