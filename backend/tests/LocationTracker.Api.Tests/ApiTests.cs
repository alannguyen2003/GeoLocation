using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LocationTracker.Api.Contracts;

namespace LocationTracker.Api.Tests;

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient CreateClient()
    {
        Skip.If(factory.SkipReason is not null, factory.SkipReason);
        return factory.CreateClient();
    }

    private static async Task<RegisterDeviceResponse> RegisterAsync(HttpClient client, string platform = "web")
    {
        var response = await client.PostAsJsonAsync("/api/devices", new { platform });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>())!;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, RegisterDeviceResponse device, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Device-Id", device.DeviceId.ToString());
        request.Headers.Add("X-Device-Token", device.DeviceToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static object NewPoint(DateTimeOffset recordedAt, double latitude = 10.7769, Guid? clientPointId = null) => new
    {
        clientPointId = clientPointId ?? Guid.NewGuid(),
        latitude,
        longitude = 106.7009,
        accuracy = 8.5,
        recordedAt,
        source = "web",
    };

    [SkippableFact]
    public async Task Health_ReportsDatabaseHealthy()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [SkippableFact]
    public async Task Register_InvalidPlatform_Returns400ProblemDetails()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/devices", new { platform = "windows" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [SkippableFact]
    public async Task Locations_WithoutHeaders_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/locations/latest");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Locations_WithWrongToken_Returns401()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);

        var response = await client.SendAsync(
            Authed(HttpMethod.Get, "/api/locations/latest", device with { DeviceToken = "wrong" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Latest_NewDevice_Returns204()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations/latest", device));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [SkippableFact]
    public async Task UploadQueryAndRetry_EndToEnd()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client, "android");
        var now = DateTimeOffset.UtcNow;
        var points = new[] { NewPoint(now.AddMinutes(-2)), NewPoint(now.AddMinutes(-1)) };

        var upload = await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device, new { points }));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal(new BatchUploadResponse(2, 0), await upload.Content.ReadFromJsonAsync<BatchUploadResponse>());

        var retry = await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device, new { points }));
        Assert.Equal(new BatchUploadResponse(0, 2), await retry.Content.ReadFromJsonAsync<BatchUploadResponse>());

        var latest = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations/latest", device));
        var latestPoint = await latest.Content.ReadFromJsonAsync<LocationPointResponse>();
        Assert.Equal(now.AddMinutes(-1).ToUnixTimeMilliseconds(), latestPoint!.RecordedAt.ToUnixTimeMilliseconds());

        var list = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations?limit=10", device));
        var listed = await list.Content.ReadFromJsonAsync<List<LocationPointResponse>>();
        Assert.Equal(2, listed!.Count);
        Assert.True(listed[0].RecordedAt > listed[1].RecordedAt);
    }

    [SkippableFact]
    public async Task Upload_ResolvesPlaceNamesAndKeepsDistrictFromThePhone()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client, "android");
        var point = new
        {
            clientPointId = Guid.NewGuid(),
            latitude = 21.0285,
            longitude = 105.8542,
            recordedAt = DateTimeOffset.UtcNow,
            source = "foreground",
            district = "Hoàn Kiếm",
        };

        await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device, new { points = new[] { point } }));
        var latest = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations/latest", device));
        var stored = await latest.Content.ReadFromJsonAsync<LocationPointResponse>();

        Assert.Equal("Hoàn Kiếm", stored!.District);
        Assert.Equal("VN", stored.CountryCode);
        Assert.Equal("Vietnam", stored.Country);
        Assert.Equal("Hanoi", stored.Region);
        Assert.False(string.IsNullOrWhiteSpace(stored.City));
    }

    [SkippableFact]
    public async Task Points_AreIsolatedPerDevice()
    {
        var client = CreateClient();
        var owner = await RegisterAsync(client);
        var other = await RegisterAsync(client);
        await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", owner,
            new { points = new[] { NewPoint(DateTimeOffset.UtcNow) } }));

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations", other));
        var listed = await response.Content.ReadFromJsonAsync<List<LocationPointResponse>>();

        Assert.Empty(listed!);
    }

    [SkippableFact]
    public async Task Upload_InvalidLatitude_Returns400WithFieldError()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device,
            new { points = new[] { NewPoint(DateTimeOffset.UtcNow, latitude: 91) } }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("points[0].latitude", out _));
    }

    [SkippableFact]
    public async Task Upload_EmptyBatch_Returns400()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device,
            new { points = Array.Empty<object>() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetPoints_LimitAbove500_Returns400()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);

        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations?limit=501", device));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task DeleteMe_RemovesDeviceAndItsPoints()
    {
        var client = CreateClient();
        var device = await RegisterAsync(client);
        await client.SendAsync(Authed(HttpMethod.Post, "/api/locations/batch", device,
            new { points = new[] { NewPoint(DateTimeOffset.UtcNow) } }));

        var delete = await client.SendAsync(Authed(HttpMethod.Delete, "/api/devices/me", device));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var after = await client.SendAsync(Authed(HttpMethod.Get, "/api/locations", device));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [SkippableFact]
    public async Task CorsPreflight_FromAllowedOrigin_AllowsDeviceHeaders()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/locations/batch");
        request.Headers.Add("Origin", ApiFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-device-id,x-device-token");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(ApiFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [SkippableFact]
    public async Task CorsPreflight_FromUnknownOrigin_IsNotAllowed()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/locations/batch");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
