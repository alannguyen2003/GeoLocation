using LocationTracker.Business.Models;
using LocationTracker.Business.Security;
using LocationTracker.Business.Validation;
using LocationTracker.Data.Entities;
using LocationTracker.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace LocationTracker.Business.Services;

public class DeviceService(
    IDeviceRepository devices,
    TimeProvider timeProvider,
    ILogger<DeviceService> logger) : IDeviceService
{
    // Avoid a database write on every request just to bump last_seen_at.
    private static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(5);

    public async Task<DeviceRegistration> RegisterAsync(string? platform, CancellationToken cancellationToken = default)
    {
        var normalized = platform?.Trim().ToLowerInvariant();
        if (normalized is null || !DevicePlatform.All.Contains(normalized))
        {
            throw new BusinessValidationException("platform", "platform must be one of: android, ios, web.");
        }

        var now = timeProvider.GetUtcNow();
        var token = DeviceTokens.Generate();
        var device = new Device
        {
            Id = Guid.CreateVersion7(),
            TokenHash = DeviceTokens.Hash(token),
            Platform = normalized,
            CreatedAt = now,
            LastSeenAt = now,
        };

        await devices.AddAsync(device, cancellationToken);
        logger.LogInformation("Registered device {DeviceId} ({Platform})", device.Id, device.Platform);

        return new DeviceRegistration(device.Id, token);
    }

    public async Task<bool> VerifyAsync(Guid deviceId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByIdAsync(deviceId, cancellationToken);
        if (device is null || !DeviceTokens.HashesEqual(device.TokenHash, tokenHash))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (now - device.LastSeenAt > LastSeenResolution)
        {
            await devices.UpdateLastSeenAsync(deviceId, now, cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var deleted = await devices.DeleteAsync(deviceId, cancellationToken);
        if (deleted)
        {
            logger.LogInformation("Deleted device {DeviceId} and all its points", deviceId);
        }

        return deleted;
    }
}
