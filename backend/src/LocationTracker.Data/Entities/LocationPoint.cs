namespace LocationTracker.Data.Entities;

public class LocationPoint
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    /// <summary>Generated on the device; used to drop duplicates when a batch is retried.</summary>
    public Guid ClientPointId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public float? AccuracyM { get; set; }

    public float? AltitudeM { get; set; }

    public float? SpeedMps { get; set; }

    public float? HeadingDeg { get; set; }

    /// <summary>Device time, UTC.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Server time.</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    public string Source { get; set; } = string.Empty;

    /// <summary>Ward or district, from the phone's own geocoder while the app was open. Best effort, often null.</summary>
    public string? District { get; set; }

    /// <summary>Nearest populated place, resolved on the server from the GeoNames dataset.</summary>
    public string? City { get; set; }

    /// <summary>Province or state (GeoNames admin1).</summary>
    public string? Region { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; set; }

    public string? Country { get; set; }
}
