using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence.Configurations;

internal sealed class UptimeHourlyRollupConfiguration : IEntityTypeConfiguration<UptimeHourlyRollup>
{
    public void Configure(EntityTypeBuilder<UptimeHourlyRollup> builder)
    {
        builder.ToTable("uptime_hourly_rollups");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.MonitoredEntityId)
            .HasColumnName("monitored_entity_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(UptimeHourlyRollup.EntityNameMaxLength)
            .IsRequired();

        builder.Property(e => e.BucketHourUtc)
            .HasColumnName("bucket_hour_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpCount)
            .HasColumnName("up_count")
            .IsRequired();

        builder.Property(e => e.DegradedCount)
            .HasColumnName("degraded_count")
            .IsRequired();

        builder.Property(e => e.DownCount)
            .HasColumnName("down_count")
            .IsRequired();

        builder.Property(e => e.UnknownCount)
            .HasColumnName("unknown_count")
            .IsRequired();

        builder.Property(e => e.TotalCount)
            .HasColumnName("total_count")
            .IsRequired();

        builder.Property(e => e.SumElapsedMs)
            .HasColumnName("sum_elapsed_ms")
            .IsRequired();

        builder.Property(e => e.MaxElapsedMs)
            .HasColumnName("max_elapsed_ms")
            .IsRequired();

        builder.Property(e => e.UptimeRatio)
            .HasColumnName("uptime_ratio")
            .HasColumnType("double");

        builder.Property(e => e.WeightedAvailability)
            .HasColumnName("weighted_availability")
            .HasColumnType("double");

        builder.Property(e => e.InsufficientData)
            .HasColumnName("insufficient_data")
            .IsRequired();

        builder.Property(e => e.ComputedAtUtc)
            .HasColumnName("computed_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Unique constraint on (entity, hour) — the natural business key for rollups.
        // The aggregation engine uses this to distinguish insert vs update.
        builder.HasIndex(e => new { e.MonitoredEntityId, e.BucketHourUtc })
            .IsUnique()
            .HasDatabaseName("ix_uptime_hourly_entity_hour");

        // Index for time-range reads (window queries).
        builder.HasIndex(e => e.BucketHourUtc)
            .HasDatabaseName("ix_uptime_hourly_bucket_hour");

        // FK to monitored_entities — cascade so cleanup is automatic.
        builder.HasOne<MonitoredEntity>()
            .WithMany()
            .HasForeignKey(e => e.MonitoredEntityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
