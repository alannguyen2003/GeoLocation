using LocationTracker.Api.Auth;
using LocationTracker.Api.Errors;
using LocationTracker.Api.Infrastructure;
using LocationTracker.Api.Places;
using LocationTracker.Api.Retention;
using LocationTracker.Business.Services;
using LocationTracker.Data;
using LocationTracker.Data.Places;
using LocationTracker.Data.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Render injects PORT (10000). Locally fall back to 8080.
var port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Data layer. This file is the only place in the Api project allowed to reference Data types.
builder.Services.AddDbContext<AppDbContext>((_, options) =>
    options.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration)));
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
// Loads the embedded GeoNames dataset (~160k places) once; it is read-only, so one instance serves every request.
builder.Services.AddSingleton<IPlaceLookup, GeoNamesPlaceLookup>();

// Business layer.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// Presentation layer.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddLocationTrackerRateLimiting(builder.Configuration);

builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddHostedService<RetentionBackgroundService>();
builder.Services.AddHostedService<PlaceBackfillBackgroundService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy addresses are not fixed, so trust the forwarded headers from any proxy.
    // This is safe only because the container is reachable solely through Render's proxy.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "DELETE")
    .WithHeaders("Content-Type", DeviceContext.DeviceIdHeader, DeviceContext.DeviceTokenHeader)
    .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

var app = builder.Build();

// Applying migrations at startup is acceptable while there is a single instance.
// If the service scales out, move this to a pre-deploy step (for example an EF Core
// migration bundle run as Render's preDeployCommand) so instances do not race each other.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Load the place dataset now rather than inside the first upload request.
app.Services.GetRequiredService<IPlaceLookup>();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseCors();
app.UseMiddleware<DeviceAuthMiddleware>();
app.UseRateLimiter();

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })
    .DisableRateLimiting();
app.MapControllers();

await app.RunAsync();

// Lets WebApplicationFactory<Program> in the integration tests find the entry point.
public partial class Program;
