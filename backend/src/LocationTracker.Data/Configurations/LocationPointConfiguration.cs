using LocationTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocationTracker.Data.Configurations;

public class LocationPointConfiguration : IEntityTypeConfiguration<LocationPoint>
{
    public void Configure(EntityTypeBuilder<LocationPoint> builder)
    {
        builder.ToTable("location_points");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(p => p.DeviceId).HasColumnName("device_id");
        builder.Property(p => p.ClientPointId).HasColumnName("client_point_id").IsRequired();
        builder.Property(p => p.Latitude).HasColumnName("latitude");
        builder.Property(p => p.Longitude).HasColumnName("longitude");
        builder.Property(p => p.AccuracyM).HasColumnName("accuracy_m");
        builder.Property(p => p.AltitudeM).HasColumnName("altitude_m");
        builder.Property(p => p.SpeedMps).HasColumnName("speed_mps");
        builder.Property(p => p.HeadingDeg).HasColumnName("heading_deg");
        builder.Property(p => p.RecordedAt).HasColumnName("recorded_at");
        builder.Property(p => p.ReceivedAt).HasColumnName("received_at");
        builder.Property(p => p.Source).HasColumnName("source").IsRequired();
        builder.Property(p => p.District).HasColumnName("district");
        builder.Property(p => p.City).HasColumnName("city");
        builder.Property(p => p.Region).HasColumnName("region");
        builder.Property(p => p.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(p => p.Country).HasColumnName("country");

        builder.HasIndex(p => new { p.DeviceId, p.ClientPointId })
            .IsUnique()
            .HasDatabaseName("ix_location_points_device_id_client_point_id");

        builder.HasIndex(p => new { p.DeviceId, p.RecordedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_location_points_device_id_recorded_at");

        // Supports the daily retention delete.
        builder.HasIndex(p => p.RecordedAt)
            .HasDatabaseName("ix_location_points_recorded_at");
    }
}
