namespace LocationTracker.Data.Entities;

public class Device
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the device token, hex encoded. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<LocationPoint> LocationPoints { get; set; } = [];
}
