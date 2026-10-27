namespace LocationTracker.Business.Models;

/// <summary>Returned once at registration. The raw token is not kept anywhere on the server.</summary>
public record DeviceRegistration(Guid DeviceId, string DeviceToken);

public record NewLocationPoint(
    Guid ClientPointId,
    double Latitude,
    double Longitude,
    float? Accuracy,
    float? Altitude,
    float? Speed,
    float? Heading,
    DateTimeOffset RecordedAt,
    string Source,
    string? District = null);

public record LocationPointModel(
    long Id,
    Guid ClientPointId,
    double Latitude,
    double Longitude,
    float? Accuracy,
    float? Altitude,
    float? Speed,
    float? Heading,
    DateTimeOffset RecordedAt,
    DateTimeOffset ReceivedAt,
    string Source,
    string? District,
    string? City,
    string? Region,
    string? CountryCode,
    string? Country);

public record BatchUploadResult(int Accepted, int Duplicates);
