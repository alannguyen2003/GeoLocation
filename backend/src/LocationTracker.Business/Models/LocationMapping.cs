using LocationTracker.Business.Validation;
using LocationTracker.Data.Entities;
using LocationTracker.Data.Places;

namespace LocationTracker.Business.Models;

internal static class LocationMapping
{
    public static LocationPoint ToEntity(
        this NewLocationPoint point,
        Guid deviceId,
        DateTimeOffset receivedAt,
        PlaceMatch? place) => new()
    {
        DeviceId = deviceId,
        ClientPointId = point.ClientPointId,
        Latitude = point.Latitude,
        Longitude = point.Longitude,
        AccuracyM = point.Accuracy,
        AltitudeM = point.Altitude,
        // Platforms report -1 when speed or heading is unknown; store that as "no value".
        SpeedMps = point.Speed is < 0 ? null : point.Speed,
        HeadingDeg = point.Heading is < 0 ? null : point.Heading,
        RecordedAt = point.RecordedAt.ToUniversalTime(),
        ReceivedAt = receivedAt.ToUniversalTime(),
        Source = point.Source,
        District = LocationRules.NormalizeDistrict(point.District),
        City = place?.City,
        Region = place?.Region,
        CountryCode = place?.CountryCode,
        Country = place?.Country,
    };

    public static LocationPointModel ToModel(this LocationPoint entity) => new(
        entity.Id,
        entity.ClientPointId,
        entity.Latitude,
        entity.Longitude,
        entity.AccuracyM,
        entity.AltitudeM,
        entity.SpeedMps,
        entity.HeadingDeg,
        entity.RecordedAt,
        entity.ReceivedAt,
        entity.Source,
        entity.District,
        entity.City,
        entity.Region,
        entity.CountryCode,
        entity.Country);
}
