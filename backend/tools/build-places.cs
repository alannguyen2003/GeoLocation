// Regenerates the place-name dataset embedded in LocationTracker.Data from GeoNames.
//
//   cd backend
//   dotnet run tools/build-places.cs
//
// Writes src/LocationTracker.Data/Places/places.tsv.gz  (lat, lon, city, country code, region)
//    and src/LocationTracker.Data/Places/countries.tsv  (country code, country name).
// Source: https://download.geonames.org/export/dump/ (CC BY 4.0, attribution "GeoNames.org").

using System.Globalization;
using System.IO.Compression;
using System.Text;

const string BaseUrl = "https://download.geonames.org/export/dump/";
var outputDir = Path.Combine("src", "LocationTracker.Data", "Places");
if (!Directory.Exists(Path.Combine("src", "LocationTracker.Data")))
{
    Console.Error.WriteLine("Run this from the backend folder: dotnet run tools/build-places.cs");
    return 1;
}
Directory.CreateDirectory(outputDir);

// Historical, abandoned, destroyed places and sections of a city are not useful as "the city you are in".
var skippedFeatureCodes = new HashSet<string> { "PPLX", "PPLH", "PPLQ", "PPLW", "PPLCH" };

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("LocationTracker-PoC-build-places/1.0");

Console.WriteLine("Downloading admin1CodesASCII.txt and countryInfo.txt...");
var regions = new Dictionary<string, string>();
foreach (var line in (await http.GetStringAsync(BaseUrl + "admin1CodesASCII.txt")).Split('\n'))
{
    var cols = line.Split('\t');
    if (cols.Length >= 2)
    {
        regions[cols[0]] = cols[1].Trim();
    }
}

var countries = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var line in (await http.GetStringAsync(BaseUrl + "countryInfo.txt")).Split('\n'))
{
    if (line.StartsWith('#'))
    {
        continue;
    }
    var cols = line.Split('\t');
    if (cols.Length >= 5 && cols[0].Length == 2)
    {
        countries[cols[0]] = cols[4].Trim();
    }
}

Console.WriteLine("Downloading cities1000.zip...");
await using var zipStream = new MemoryStream(await http.GetByteArrayAsync(BaseUrl + "cities1000.zip"));
using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
using var reader = new StreamReader(zip.GetEntry("cities1000.txt")!.Open(), Encoding.UTF8);

var placesPath = Path.Combine(outputDir, "places.tsv.gz");
await using (var file = File.Create(placesPath))
await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
await using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)) { NewLine = "\n" })
{
    var kept = 0;
    var skipped = 0;
    while (await reader.ReadLineAsync() is { } line)
    {
        // geonameid, name, asciiname, alternatenames, latitude, longitude, feature class,
        // feature code, country code, cc2, admin1 code, ...
        var cols = line.Split('\t');
        if (cols.Length < 11 || cols[6] != "P" || skippedFeatureCodes.Contains(cols[7]) || cols[8].Length != 2)
        {
            skipped++;
            continue;
        }

        var lat = double.Parse(cols[4], CultureInfo.InvariantCulture);
        var lon = double.Parse(cols[5], CultureInfo.InvariantCulture);
        var region = regions.GetValueOrDefault($"{cols[8]}.{cols[10]}", string.Empty);
        // Four decimals is about 11 m, far finer than "nearest city" needs.
        await writer.WriteLineAsync(string.Join('\t',
            lat.ToString("F4", CultureInfo.InvariantCulture),
            lon.ToString("F4", CultureInfo.InvariantCulture),
            cols[1].Replace('\t', ' '),
            cols[8],
            region));
        kept++;
    }
    Console.WriteLine($"Kept {kept} places, skipped {skipped}.");
}

await File.WriteAllLinesAsync(
    Path.Combine(outputDir, "countries.tsv"),
    countries.Select(c => $"{c.Key}\t{c.Value}"),
    new UTF8Encoding(false));

Console.WriteLine($"Wrote {placesPath} ({new FileInfo(placesPath).Length / 1024} KB) and {countries.Count} countries.");
return 0;
