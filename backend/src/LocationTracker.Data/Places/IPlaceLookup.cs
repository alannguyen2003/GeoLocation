namespace LocationTracker.Data.Places;

/// <param name="City">Nearest populated place. In big cities this can be a ward or district, because GeoNames lists those as places too.</param>
/// <param name="Region">First-level administrative division (province, state), or null when GeoNames has none.</param>
public sealed record PlaceMatch(string City, string? Region, string CountryCode, string Country, double DistanceKm);

/// <summary>Offline reverse geocoding: the nearest known populated place to a coordinate.</summary>
public interface IPlaceLookup
{
    /// <summary>The nearest place within <paramref name="maxDistanceKm"/>, or null (open sea, polar regions).</summary>
    PlaceMatch? FindNearest(double latitude, double longitude, double maxDistanceKm);
}
