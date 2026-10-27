using System.Globalization;
using System.IO.Compression;

namespace LocationTracker.Data.Places;

/// <summary>
/// Nearest-place lookup over the GeoNames "cities1000" dataset embedded in this assembly
/// (regenerate with backend/tools/build-places.cs). Loaded once into memory and indexed in a
/// 1° x 1° grid; a lookup checks only the cells within the search radius.
/// Data: GeoNames.org, CC BY 4.0.
/// </summary>
public sealed class GeoNamesPlaceLookup : IPlaceLookup
{
    private const string PlacesResource = "LocationTracker.Data.Places.places.tsv.gz";
    private const string CountriesResource = "LocationTracker.Data.Places.countries.tsv";
    private const double EarthRadiusKm = 6371.0;
    private const double KmPerDegreeLatitude = 111.32;

    private readonly Place[] _places;
    private readonly Dictionary<int, int[]> _grid;
    private readonly Dictionary<string, string> _countries;

    private readonly record struct Place(double Latitude, double Longitude, string City, string CountryCode, string? Region);

    public GeoNamesPlaceLookup()
    {
        _countries = LoadCountries();
        _places = LoadPlaces();
        _grid = _places
            .Select((place, index) => (Cell: CellKey(LatCell(place.Latitude), LonCell(place.Longitude)), Index: index))
            .GroupBy(x => x.Cell)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Index).ToArray());
    }

    public int Count => _places.Length;

    public PlaceMatch? FindNearest(double latitude, double longitude, double maxDistanceKm)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || maxDistanceKm <= 0)
        {
            return null;
        }

        var bestIndex = -1;
        var bestKm = maxDistanceKm;

        foreach (var index in CandidatesNear(latitude, longitude, maxDistanceKm))
        {
            var place = _places[index];
            var km = DistanceKm(latitude, longitude, place.Latitude, place.Longitude);
            if (km <= bestKm)
            {
                bestKm = km;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            return null;
        }

        var best = _places[bestIndex];
        return new PlaceMatch(
            best.City,
            best.Region,
            best.CountryCode,
            _countries.GetValueOrDefault(best.CountryCode, best.CountryCode),
            bestKm);
    }

    /// <summary>Indexes of every place in the grid cells that the search radius can reach.</summary>
    private IEnumerable<int> CandidatesNear(double latitude, double longitude, double maxDistanceKm)
    {
        var latSpan = (int)Math.Ceiling(maxDistanceKm / KmPerDegreeLatitude);
        // A degree of longitude shrinks towards the poles, so more cells are needed there.
        var cosLat = Math.Cos(latitude * Math.PI / 180);
        var lonSpan = cosLat < 0.01 ? 180 : Math.Min(180, (int)Math.Ceiling(maxDistanceKm / (KmPerDegreeLatitude * cosLat)));

        var centerLat = LatCell(latitude);
        var centerLon = LonCell(longitude);
        // Wrap around the antimeridian; near the poles every longitude cell is in range.
        var lonCells = lonSpan >= 180
            ? Enumerable.Range(0, 360).ToArray()
            : Enumerable.Range(-lonSpan, 2 * lonSpan + 1).Select(offset => ((centerLon + offset) % 360 + 360) % 360).ToArray();

        for (var latCell = Math.Max(0, centerLat - latSpan); latCell <= Math.Min(179, centerLat + latSpan); latCell++)
        {
            foreach (var lonCell in lonCells)
            {
                if (_grid.TryGetValue(CellKey(latCell, lonCell), out var indexes))
                {
                    foreach (var index in indexes)
                    {
                        yield return index;
                    }
                }
            }
        }
    }

    private static int LatCell(double latitude) => Math.Clamp((int)Math.Floor(latitude) + 90, 0, 179);

    private static int LonCell(double longitude) => ((int)Math.Floor(longitude) + 180 + 360) % 360;

    private static int CellKey(int latCell, int lonCell) => latCell * 360 + lonCell;

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double toRad = Math.PI / 180;
        var dLat = (lat2 - lat1) * toRad;
        var dLon = (lon2 - lon1) * toRad;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(lat1 * toRad) * Math.Cos(lat2 * toRad) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static Stream OpenResource(string name) =>
        typeof(GeoNamesPlaceLookup).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded resource {name} is missing. Run backend/tools/build-places.cs.");

    private static Dictionary<string, string> LoadCountries()
    {
        using var reader = new StreamReader(OpenResource(CountriesResource));
        var countries = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var cols = line.Split('\t');
            if (cols.Length == 2)
            {
                countries[cols[0]] = cols[1];
            }
        }

        return countries;
    }

    private static Place[] LoadPlaces()
    {
        using var reader = new StreamReader(new GZipStream(OpenResource(PlacesResource), CompressionMode.Decompress));
        var places = new List<Place>(170_000);
        // Country codes and region names repeat a lot; share one string instance for each.
        var shared = new Dictionary<string, string>(StringComparer.Ordinal);
        string Share(string value)
        {
            if (shared.TryGetValue(value, out var existing))
            {
                return existing;
            }
            shared[value] = value;
            return value;
        }

        while (reader.ReadLine() is { } line)
        {
            var cols = line.Split('\t');
            if (cols.Length < 5)
            {
                continue;
            }

            places.Add(new Place(
                double.Parse(cols[0], CultureInfo.InvariantCulture),
                double.Parse(cols[1], CultureInfo.InvariantCulture),
                cols[2],
                Share(cols[3]),
                cols[4].Length > 0 ? Share(cols[4]) : null));
        }

        return places.ToArray();
    }
}
