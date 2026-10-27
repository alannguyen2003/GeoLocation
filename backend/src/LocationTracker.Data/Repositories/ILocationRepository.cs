using LocationTracker.Data.Entities;
using LocationTracker.Data.Places;

namespace LocationTracker.Data.Repositories;

public interface ILocationRepository
{
    /// <summary>
    /// Inserts the points and silently skips any whose (device_id, client_point_id) already exists.
    /// Returns the number of rows actually inserted.
    /// </summary>
    Task<int> InsertIgnoringDuplicatesAsync(IReadOnlyList<LocationPoint> points, CancellationToken cancellationToken = default);

    /// <summary>Points of one device, newest first.</summary>
    Task<IReadOnlyList<LocationPoint>> GetForDeviceAsync(
        Guid deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken = default);

    Task<LocationPoint?> GetLatestAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    /// <summary>Points stored before place names existed (or with no place in range), oldest first.</summary>
    Task<IReadOnlyList<PointCoordinates>> GetWithoutPlaceAsync(long afterId, int take, CancellationToken cancellationToken = default);

    Task SetPlaceAsync(long id, PlaceMatch place, CancellationToken cancellationToken = default);
}

public sealed record PointCoordinates(long Id, double Latitude, double Longitude);
