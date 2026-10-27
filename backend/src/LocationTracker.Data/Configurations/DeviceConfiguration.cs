using LocationTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocationTracker.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(d => d.Platform).HasColumnName("platform").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.LastSeenAt).HasColumnName("last_seen_at");

        builder.HasMany(d => d.LocationPoints)
            .WithOne(p => p.Device)
            .HasForeignKey(p => p.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
