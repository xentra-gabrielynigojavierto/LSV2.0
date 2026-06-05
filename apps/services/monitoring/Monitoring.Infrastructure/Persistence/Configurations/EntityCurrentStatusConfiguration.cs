using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence.Configurations;

internal sealed class EntityCurrentStatusConfiguration : IEntityTypeConfiguration<EntityCurrentStatus>
{
    public void Configure(EntityTypeBuilder<EntityCurrentStatus> builder)
    {
        builder.ToTable("entity_current_status");

        // PK is also the FK to monitored_entities(id) — at most one
        // current-status row per monitored entity.
        builder.HasKey(e => e.MonitoredEntityId);

        builder.Property(e => e.MonitoredEntityId)
            .HasColumnName("monitored_entity_id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.CurrentStatus)
            .HasColumnName("current_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LastOutcome)
            .HasColumnName("last_outcome")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LastStatusCode)
            .HasColumnName("last_status_code");

        builder.Property(e => e.LastElapsedMs)
            .HasColumnName("last_elapsed_ms")
            .IsRequired();

        builder.Property(e => e.LastCheckedAtUtc)
            .HasColumnName("last_checked_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.LastMessage)
            .HasColumnName("last_message")
            .HasMaxLength(EntityCurrentStatus.LastMessageMaxLength)
            .IsRequired();

        builder.Property(e => e.LastErrorType)
            .HasColumnName("last_error_type")
            .HasMaxLength(EntityCurrentStatus.LastErrorTypeMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Foreign key to monitored_entities. No navigation property on
        // either side — current-status is queried directly. ON DELETE
        // CASCADE so removing a monitored entity also removes its
        // current-status row, matching the policy on check_results.
        builder.HasOne<MonitoredEntity>()
            .WithMany()
            .HasForeignKey(e => e.MonitoredEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on current_status to support future "show me everything
        // that's Down right now" reads without a scan.
        builder.HasIndex(e => e.CurrentStatus)
            .HasDatabaseName("ix_entity_current_status_current_status");
    }
}
