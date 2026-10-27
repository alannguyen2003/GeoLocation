using LocationTracker.Business.Models;

namespace LocationTracker.Business.Services;

public interface ILocationService
{
    Task<BatchUploadResult> UploadBatchAsync(
        Guid deviceId,
        IReadOnlyList<NewLocationPoint>? points,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationPointModel>> GetPointsAsync(
        Guid deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit,
        CancellationToken cancellationToken = default);

    Task<LocationPointModel?> GetLatestAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every point recorded more than <paramref name="retentionDays"/> days ago.</summary>
    Task<int> DeleteExpiredAsync(int retentionDays, CancellationToken cancellationToken = default);

    /// <summary>Resolves city, region and country for stored points that have none yet.</summary>
    Task<int> FillMissingPlacesAsync(CancellationToken cancellationToken = default);
}
