using LocationTracker.Business.Models;
using LocationTracker.Business.Services;
using LocationTracker.Business.Tests.Fakes;
using LocationTracker.Business.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LocationTracker.Business.Tests;

public class LocationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DeviceId = Guid.NewGuid();

    private readonly FakeLocationRepository _repository = new();
    private readonly FakePlaceLookup _places = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly LocationService _service;

    public LocationServiceTests()
    {
        _service = new LocationService(_repository, _places, _time, NullLogger<LocationService>.Instance);
    }

    private static NewLocationPoint Point(
        Guid? clientPointId = null,
        double latitude = 10.7769,
        double longitude = 106.7009,
        float? accuracy = 10,
        DateTimeOffset? recordedAt = null,
        string source = LocationSource.Foreground,
        float? speed = null,
        float? heading = null,
        string? district = null) =>
        new(clientPointId ?? Guid.NewGuid(), latitude, longitude, accuracy, null, speed, heading, recordedAt ?? Now, source, district);

    private static List<NewLocationPoint> Points(int count) =>
        Enumerable.Range(0, count).Select(_ => Point()).ToList();

    [Fact]
    public async Task UploadBatch_ValidPoints_AreAllAccepted()
    {
        var result = await _service.UploadBatchAsync(DeviceId, Points(3));

        Assert.Equal(new BatchUploadResult(3, 0), result);
        Assert.Equal(3, _repository.Points.Count);
        Assert.All(_repository.Points, p => Assert.Equal(Now, p.ReceivedAt));
    }

    [Fact]
    public async Task UploadBatch_RetriedBatch_CountsEveryPointAsDuplicate()
    {
        var batch = Points(5);
        await _service.UploadBatchAsync(DeviceId, batch);

        var retry = await _service.UploadBatchAsync(DeviceId, batch);

        Assert.Equal(new BatchUploadResult(0, 5), retry);
        Assert.Equal(5, _repository.Points.Count);
    }

    [Fact]
    public async Task UploadBatch_DuplicateInsideBatch_IsStoredOnce()
    {
        var id = Guid.NewGuid();
        var batch = new List<NewLocationPoint> { Point(id), Point(id), Point() };

        var result = await _service.UploadBatchAsync(DeviceId, batch);

        Assert.Equal(new BatchUploadResult(2, 1), result);
        Assert.Single(_repository.Points, p => p.ClientPointId == id);
    }

    [Fact]
    public async Task UploadBatch_SameClientPointIdOnAnotherDevice_IsNotADuplicate()
    {
        var id = Guid.NewGuid();
        await _service.UploadBatchAsync(DeviceId, [Point(id)]);

        var result = await _service.UploadBatchAsync(Guid.NewGuid(), [Point(id)]);

        Assert.Equal(1, result.Accepted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task UploadBatch_SizeOutsideOneToHundred_IsRejected(int count)
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.UploadBatchAsync(DeviceId, Points(count)));

        Assert.Contains("points", ex.Errors.Keys);
        Assert.Empty(_repository.Points);
    }

    [Fact]
    public async Task UploadBatch_NullPoints_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UploadBatchAsync(DeviceId, null));
    }

    [Fact]
    public async Task UploadBatch_HundredPoints_IsAccepted()
    {
        var result = await _service.UploadBatchAsync(DeviceId, Points(100));

        Assert.Equal(100, result.Accepted);
    }

    [Theory]
    [InlineData(-90.0001, 0, "latitude")]
    [InlineData(90.0001, 0, "latitude")]
    [InlineData(double.NaN, 0, "latitude")]
    [InlineData(0, -180.0001, "longitude")]
    [InlineData(0, 180.0001, "longitude")]
    [InlineData(0, double.PositiveInfinity, "longitude")]
    public async Task UploadBatch_CoordinatesOutOfRange_AreRejected(double latitude, double longitude, string field)
    {
        var batch = new List<NewLocationPoint> { Point(), Point(latitude: latitude, longitude: longitude) };

        var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UploadBatchAsync(DeviceId, batch));

        Assert.Contains($"points[1].{field}", ex.Errors.Keys);
        Assert.Empty(_repository.Points);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public async Task UploadBatch_CoordinatesOnTheBoundary_AreAccepted(double latitude, double longitude)
    {
        var result = await _service.UploadBatchAsync(DeviceId, [Point(latitude: latitude, longitude: longitude)]);

        Assert.Equal(1, result.Accepted);
    }

    [Fact]
    public async Task UploadBatch_RecordedMoreThanFiveMinutesAhead_IsRejected()
    {
        var batch = new List<NewLocationPoint> { Point(recordedAt: Now.AddMinutes(5).AddSeconds(1)) };

        var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UploadBatchAsync(DeviceId, batch));

        Assert.Contains("points[0].recordedAt", ex.Errors.Keys);
    }

    [Fact]
    public async Task UploadBatch_RecordedSlightlyAhead_IsAccepted()
    {
        var result = await _service.UploadBatchAsync(DeviceId, [Point(recordedAt: Now.AddMinutes(4))]);

        Assert.Equal(1, result.Accepted);
    }

    [Fact]
    public async Task UploadBatch_RecordedMoreThanSevenDaysAgo_IsRejected()
    {
        var batch = new List<NewLocationPoint> { Point(recordedAt: Now.AddDays(-7).AddSeconds(-1)) };

        var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.UploadBatchAsync(DeviceId, batch));

        Assert.Contains("points[0].recordedAt", ex.Errors.Keys);
    }

    [Fact]
    public async Task UploadBatch_NegativeAccuracy_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.UploadBatchAsync(DeviceId, [Point(accuracy: -1)]));

        Assert.Contains("points[0].accuracy", ex.Errors.Keys);
    }

    [Fact]
    public async Task UploadBatch_MissingAccuracy_IsAccepted()
    {
        var result = await _service.UploadBatchAsync(DeviceId, [Point(accuracy: null)]);

        Assert.Equal(1, result.Accepted);
    }

    [Fact]
    public async Task UploadBatch_UnknownSource_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.UploadBatchAsync(DeviceId, [Point(source: "satellite")]));

        Assert.Contains("points[0].source", ex.Errors.Keys);
    }

    [Fact]
    public async Task UploadBatch_EmptyClientPointId_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.UploadBatchAsync(DeviceId, [Point(clientPointId: Guid.Empty)]));

        Assert.Contains("points[0].clientPointId", ex.Errors.Keys);
    }

    [Fact]
    public async Task UploadBatch_NegativeSpeedAndHeading_AreStoredAsUnknown()
    {
        await _service.UploadBatchAsync(DeviceId, [Point(speed: -1, heading: -1)]);

        var stored = Assert.Single(_repository.Points);
        Assert.Null(stored.SpeedMps);
        Assert.Null(stored.HeadingDeg);
    }

    [Fact]
    public async Task GetPoints_ReturnsOnlyOwnPointsNewestFirst()
    {
        await _service.UploadBatchAsync(DeviceId, [
            Point(recordedAt: Now.AddMinutes(-10)),
            Point(recordedAt: Now.AddMinutes(-1)),
            Point(recordedAt: Now.AddMinutes(-5)),
        ]);
        await _service.UploadBatchAsync(Guid.NewGuid(), [Point()]);

        var points = await _service.GetPointsAsync(DeviceId, null, null, null);

        Assert.Equal(3, points.Count);
        Assert.Equal(
            [Now.AddMinutes(-1), Now.AddMinutes(-5), Now.AddMinutes(-10)],
            points.Select(p => p.RecordedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetPoints_LimitOutOfRange_IsRejected(int limit)
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.GetPointsAsync(DeviceId, null, null, limit));

        Assert.Contains("limit", ex.Errors.Keys);
    }

    [Fact]
    public async Task GetPoints_FromAfterTo_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessValidationException>(
            () => _service.GetPointsAsync(DeviceId, Now, Now.AddHours(-1), null));
    }

    [Fact]
    public async Task GetLatest_NoPoints_ReturnsNull()
    {
        Assert.Null(await _service.GetLatestAsync(DeviceId));
    }

    [Fact]
    public async Task UploadBatch_PointNearAKnownPlace_GetsCityRegionAndCountry()
    {
        await _service.UploadBatchAsync(DeviceId, [Point(latitude: 21.03, longitude: 105.85)]);

        var stored = Assert.Single(_repository.Points);
        Assert.Equal("Hanoi", stored.City);
        Assert.Equal("Hanoi", stored.Region);
        Assert.Equal("VN", stored.CountryCode);
        Assert.Equal("Vietnam", stored.Country);
    }

    [Fact]
    public async Task UploadBatch_PointFarFromAnyPlace_IsStoredWithoutPlace()
    {
        var result = await _service.UploadBatchAsync(DeviceId, [Point(latitude: 0, longitude: -150)]);

        Assert.Equal(1, result.Accepted);
        var stored = Assert.Single(_repository.Points);
        Assert.Null(stored.City);
        Assert.Null(stored.CountryCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" Phường Bến Nghé ", "Phường Bến Nghé")]
    public async Task UploadBatch_District_IsTrimmedAndEmptyBecomesNull(string? district, string? expected)
    {
        await _service.UploadBatchAsync(DeviceId, [Point(district: district)]);

        Assert.Equal(expected, Assert.Single(_repository.Points).District);
    }

    [Fact]
    public async Task UploadBatch_OverlongDistrict_IsShortenedNotRejected()
    {
        var result = await _service.UploadBatchAsync(DeviceId, [Point(district: new string('x', 500))]);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(LocationRules.MaxDistrictLength, Assert.Single(_repository.Points).District!.Length);
    }

    [Fact]
    public async Task FillMissingPlaces_ResolvesPointsStoredWithoutPlace()
    {
        _places.Enabled = false;
        await _service.UploadBatchAsync(DeviceId, [Point(latitude: 21.03, longitude: 105.85), Point(latitude: 0, longitude: -150)]);
        _places.Enabled = true;

        var filled = await _service.FillMissingPlacesAsync();

        Assert.Equal(1, filled);
        Assert.Single(_repository.Points, p => p.City == "Hanoi");
        Assert.Single(_repository.Points, p => p.City is null);
    }

    [Fact]
    public async Task GetPoints_ReturnsPlaceFields()
    {
        await _service.UploadBatchAsync(DeviceId, [Point(latitude: 21.03, longitude: 105.85, district: "Hoàn Kiếm")]);

        var point = Assert.Single(await _service.GetPointsAsync(DeviceId, null, null, null));

        Assert.Equal(("Hoàn Kiếm", "Hanoi", "Hanoi", "VN", "Vietnam"), (point.District, point.City, point.Region, point.CountryCode, point.Country));
    }

    [Fact]
    public async Task DeleteExpired_RemovesOnlyPointsOlderThanRetention()
    {
        await _service.UploadBatchAsync(DeviceId, [Point(recordedAt: Now.AddDays(-6)), Point(recordedAt: Now.AddDays(-1))]);
        _time.Advance(TimeSpan.FromDays(3));

        var deleted = await _service.DeleteExpiredAsync(retentionDays: 5);

        Assert.Equal(1, deleted);
        var remaining = Assert.Single(_repository.Points);
        Assert.Equal(Now.AddDays(-1), remaining.RecordedAt);
    }
}
