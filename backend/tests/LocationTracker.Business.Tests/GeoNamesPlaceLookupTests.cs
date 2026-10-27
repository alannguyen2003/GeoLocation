using LocationTracker.Data.Places;

namespace LocationTracker.Business.Tests;

/// <summary>Runs against the real embedded GeoNames dataset.</summary>
public class GeoNamesPlaceLookupTests
{
    // Loading ~160k places takes a moment; share one instance across the tests.
    private static readonly Lazy<GeoNamesPlaceLookup> Lookup = new(() => new GeoNamesPlaceLookup());

    [Fact]
    public void Dataset_IsLoaded()
    {
        Assert.True(Lookup.Value.Count > 100_000);
    }

    [Theory]
    [InlineData(21.0285, 105.8542, "VN", "Hanoi")]                       // Hoan Kiem Lake
    [InlineData(10.7769, 106.7009, "VN", "Ho Chi Minh City (HCMC)")]     // District 1
    [InlineData(16.0544, 108.2022, "VN", "Da Nang City")]
    [InlineData(35.6812, 139.7671, "JP", "Tokyo")]
    [InlineData(48.8584, 2.2945, "FR", "Île-de-France")]
    public void FindNearest_KnownCities_ReturnCountryAndRegion(double lat, double lon, string countryCode, string region)
    {
        var place = Lookup.Value.FindNearest(lat, lon, 100);

        Assert.NotNull(place);
        Assert.Equal(countryCode, place.CountryCode);
        Assert.Equal(region, place.Region);
        Assert.False(string.IsNullOrWhiteSpace(place.City));
        Assert.True(place.DistanceKm < 15, $"{place.City} is {place.DistanceKm:F1} km away");
    }

    [Fact]
    public void FindNearest_CountryName_ComesFromCountryList()
    {
        Assert.Equal("Vietnam", Lookup.Value.FindNearest(21.0285, 105.8542, 100)!.Country);
    }

    [Fact]
    public void FindNearest_MiddleOfThePacific_ReturnsNull()
    {
        Assert.Null(Lookup.Value.FindNearest(0, -150, 100));
    }

    [Fact]
    public void FindNearest_AcrossTheAntimeridian_StillFindsTheNearestPlace()
    {
        // Fiji has places on both sides of 180°; searching from just west of it must also see the east side.
        var west = Lookup.Value.FindNearest(-16.8, 179.999, 100);
        var east = Lookup.Value.FindNearest(-16.8, -179.999, 100);

        Assert.NotNull(west);
        Assert.NotNull(east);
        Assert.Equal("FJ", west.CountryCode);
        Assert.Equal(west.City, east.City);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void FindNearest_InvalidCoordinates_ReturnsNull(double lat, double lon)
    {
        Assert.Null(Lookup.Value.FindNearest(lat, lon, 100));
    }
}
