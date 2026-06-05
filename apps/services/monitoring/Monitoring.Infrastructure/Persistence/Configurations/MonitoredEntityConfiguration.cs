using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence.Configurations;

internal sealed class MonitoredEntityConfiguration : IEntityTypeConfiguration<MonitoredEntity>
{
    public void Configure(EntityTypeBuilder<MonitoredEntity> builder)
    {
        builder.ToTable("monitored_entities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(MonitoredEntity.NameMaxLength)
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.MonitoringType)
            .HasColumnName("monitoring_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Target)
            .HasColumnName("target")
            .HasMaxLength(MonitoredEntity.TargetMaxLength)
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(e => e.Scope)
            .HasColumnName("scope")
            .HasMaxLength(MonitoredEntity.ScopeMaxLength)
            .IsRequired();

        builder.Property(e => e.ImpactLevel)
            .HasColumnName("impact_level")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(e => e.Name).HasDatabaseName("ix_monitored_entities_name");
        builder.HasIndex(e => new { e.EntityType, e.MonitoringType })
            .HasDatabaseName("ix_monitored_entities_entity_type_monitoring_type");
    }
}
