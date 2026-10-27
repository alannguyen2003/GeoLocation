using System.ComponentModel.DataAnnotations;
using LocationTracker.Business.Models;

namespace LocationTracker.Api.Contracts;

public class LocationBatchRequest
{
    [Required]
    public List<LocationPointRequest>? Points { get; init; }
}

public class LocationPointRequest
{
    [Required]
    public Guid? ClientPointId { get; init; }

    [Required]
    public double? Latitude { get; init; }

    [Required]
    public double? Longitude { get; init; }

    public float? Accuracy { get; init; }

    public float? Altitude { get; init; }

    public float? Speed { get; init; }

    public float? Heading { get; init; }

    [Required]
    public DateTimeOffset? RecordedAt { get; init; }

    [Required]
    [MaxLength(16)]
    public string? Source { get; init; }

    /// <summary>Optional ward/district from the phone's geocoder. Not length-checked here: the Business layer shortens it.</summary>
    public string? District { get; init; }

    // [Required] has already run by the time this is called, so the null-forgiving operators are safe.
    public NewLocationPoint ToModel() => new(
        ClientPointId!.Value,
        Latitude!.Value,
        Longitude!.Value,
        Accuracy,
        Altitude,
        Speed,
        Heading,
        RecordedAt!.Value,
        Source!,
        District);
}

public record BatchUploadResponse(int Accepted, int Duplicates);

public record LocationPointResponse(
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
    string? Country)
{
    public static LocationPointResponse From(LocationPointModel model) => new(
        model.Id,
        model.ClientPointId,
        model.Latitude,
        model.Longitude,
        model.Accuracy,
        model.Altitude,
        model.Speed,
        model.Heading,
        model.RecordedAt,
        model.ReceivedAt,
        model.Source,
        model.District,
        model.City,
        model.Region,
        model.CountryCode,
        model.Country);
}
