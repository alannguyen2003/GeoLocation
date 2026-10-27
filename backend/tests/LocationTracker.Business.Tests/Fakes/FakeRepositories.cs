using LocationTracker.Data.Entities;
using LocationTracker.Data.Places;
using LocationTracker.Data.Repositories;

namespace LocationTracker.Business.Tests.Fakes;

public class FakeDeviceRepository : IDeviceRepository
{
    public Dictionary<Guid, Device> Devices { get; } = [];

    public Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        Devices.Add(device.Id, device);
        return Task.CompletedTask;
    }

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Devices.GetValueOrDefault(id));

    public Task UpdateLastSeenAsync(Guid id, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
    {
        if (Devices.TryGetValue(id, out var device))
        {
            device.LastSeenAt = lastSeenAt;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Devices.Remove(id));
}

/// <summary>Mirrors the unique (device_id, client_point_id) index of the real table.</summary>
public class FakeLocationRepository : ILocationRepository
{
    private long _nextId = 1;

    public List<LocationPoint> Points { get; } = [];

    public Task<int> InsertIgnoringDuplicatesAsync(IReadOnlyList<LocationPoint> points, CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        foreach (var point in points)
        {
            if (Points.Any(p => p.DeviceId == point.DeviceId && p.ClientPointId == point.ClientPointId))
            {
                continue;
            }

            point.Id = _nextId++;
            Points.Add(point);
            inserted++;
        }

        return Task.FromResult(inserted);
    }

    public Task<IReadOnlyList<LocationPoint>> GetForDeviceAsync(
        Guid deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LocationPoint> result = Points
            .Where(p => p.DeviceId == deviceId)
            .Where(p => from is null || p.RecordedAt >= from)
            .Where(p => to is null || p.RecordedAt <= to)
            .OrderByDescending(p => p.RecordedAt)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<LocationPoint?> GetLatestAsync(Guid deviceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Points
            .Where(p => p.DeviceId == deviceId)
            .OrderByDescending(p => p.RecordedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault());

    public Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Task.FromResult(Points.RemoveAll(p => p.RecordedAt < cutoff));

    public Task<IReadOnlyList<PointCoordinates>> GetWithoutPlaceAsync(long afterId, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PointCoordinates> result = Points
            .Where(p => p.Id > afterId && p.CountryCode is null)
            .OrderBy(p => p.Id)
            .Take(take)
            .Select(p => new PointCoordinates(p.Id, p.Latitude, p.Longitude))
            .ToList();
        return Task.FromResult(result);
    }

    public Task SetPlaceAsync(long id, PlaceMatch place, CancellationToken cancellationToken = default)
    {
        var point = Points.Single(p => p.Id == id);
        point.City = place.City;
        point.Region = place.Region;
        point.CountryCode = place.CountryCode;
        point.Country = place.Country;
        return Task.CompletedTask;
    }
}

/// <summary>Knows one place: anything within the radius of it matches, everything else is "open sea".</summary>
public class FakePlaceLookup : IPlaceLookup
{
    public static readonly PlaceMatch Hanoi = new("Hanoi", "Hanoi", "VN", "Vietnam", 0);

    public bool Enabled { get; set; } = true;

    public PlaceMatch? FindNearest(double latitude, double longitude, double maxDistanceKm) =>
        Enabled && Math.Abs(latitude - 21.03) < 1 && Math.Abs(longitude - 105.85) < 1 ? Hanoi : null;
}
