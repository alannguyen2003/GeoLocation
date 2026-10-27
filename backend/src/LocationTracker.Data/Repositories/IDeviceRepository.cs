using LocationTracker.Data.Entities;

namespace LocationTracker.Data.Repositories;

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken cancellationToken = default);

    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateLastSeenAsync(Guid id, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);

    /// <summary>Deletes the device; its points go with it through the cascading foreign key.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
