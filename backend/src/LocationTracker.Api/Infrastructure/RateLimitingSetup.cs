using System.Globalization;
using System.Threading.RateLimiting;
using LocationTracker.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocationTracker.Api.Infrastructure;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int RequestsPerMinutePerDevice { get; set; } = 60;

    public int RegistrationsPerHourPerIp { get; set; } = 10;
}

public static class RateLimitingSetup
{
    public const string RegistrationPolicy = "registration";

    /// <summary>
    /// Global limit partitioned by the authenticated device ID (falling back to the client IP),
    /// plus a stricter per-IP policy for registration. Must run after <see cref="DeviceAuthMiddleware"/>
    /// so the device ID is known.
    /// </summary>
    public static IServiceCollection AddLocationTrackerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var key = context.GetDeviceIdOrNull() is { } deviceId
                    ? $"device:{deviceId}"
                    : $"ip:{ClientIp(context)}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.RequestsPerMinutePerDevice,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            options.AddPolicy(RegistrationPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter($"ip:{ClientIp(context)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.RegistrationsPerHourPerIp,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var context = rejected.HttpContext;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = context.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Try again later.",
                    },
                });
            };
        });
    }

    // RemoteIpAddress already reflects X-Forwarded-For because UseForwardedHeaders runs first.
    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
