using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence.Configurations;

internal sealed class MonitoringAlertConfiguration : IEntityTypeConfiguration<MonitoringAlert>
{
    public void Configure(EntityTypeBuilder<MonitoringAlert> builder)
    {
        builder.ToTable("monitoring_alerts");

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
            .HasMaxLength(MonitoringAlert.EntityNameMaxLength)
            .IsRequired();

        builder.Property(e => e.Scope)
            .HasColumnName("scope")
            .HasMaxLength(MonitoringAlert.ScopeMaxLength)
            .IsRequired();

        builder.Property(e => e.ImpactLevel)
            .HasColumnName("impact_level")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.CurrentStatus)
            .HasColumnName("current_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.AlertType)
            .HasColumnName("alert_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.TriggeredAtUtc)
            .HasColumnName("triggered_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.Message)
            .HasColumnName("message")
            .HasMaxLength(MonitoringAlert.MessageMaxLength)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // FK to monitored_entities. No navigation property on either side
        // — alerts are queried directly. ON DELETE CASCADE so removing a
        // monitored entity removes its alert history; matches the policy
        // on check_results and entity_current_status.
        builder.HasOne<MonitoredEntity>()
            .WithMany()
            .HasForeignKey(e => e.MonitoredEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backs the dedup query: "is there an active StatusDown for this
        // entity?" and the future "show me everything actively alerting"
        // read path. Intentionally non-unique — see report §6 for why a
        // unique index is the wrong tool here.
        builder.HasIndex(e => new { e.MonitoredEntityId, e.AlertType, e.IsActive })
            .HasDatabaseName("ix_monitoring_alerts_entity_type_active");

        builder.HasIndex(e => e.TriggeredAtUtc)
            .HasDatabaseName("ix_monitoring_alerts_triggered_at_utc");
    }
}
