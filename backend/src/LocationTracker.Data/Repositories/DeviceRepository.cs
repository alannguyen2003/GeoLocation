using LocationTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocationTracker.Data.Repositories;

public class DeviceRepository(AppDbContext db) : IDeviceRepository
{
    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task UpdateLastSeenAsync(Guid id, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) =>
        db.Devices
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, lastSeenAt), cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Devices.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
