using LocationTracker.Business.Models;

namespace LocationTracker.Business.Services;

public interface IDeviceService
{
    Task<DeviceRegistration> RegisterAsync(string? platform, CancellationToken cancellationToken = default);

    /// <summary>True when the device exists and <paramref name="tokenHash"/> matches the stored hash.</summary>
    Task<bool> VerifyAsync(Guid deviceId, string tokenHash, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
