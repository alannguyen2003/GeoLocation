using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocationTracker.Api.Infrastructure;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Writes e.g. <c>{"status":"Healthy","checks":{"database":"Healthy"}}</c>. No exception details.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
