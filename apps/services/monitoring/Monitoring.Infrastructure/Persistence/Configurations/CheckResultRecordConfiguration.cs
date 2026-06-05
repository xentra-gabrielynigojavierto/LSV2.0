using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence.Configurations;

internal sealed class CheckResultRecordConfiguration : IEntityTypeConfiguration<CheckResultRecord>
{
    public void Configure(EntityTypeBuilder<CheckResultRecord> builder)
    {
        builder.ToTable("check_results");

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
            .HasMaxLength(CheckResultRecord.EntityNameMaxLength)
            .IsRequired();

        builder.Property(e => e.MonitoringType)
            .HasColumnName("monitoring_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Target)
            .HasColumnName("target")
            .HasMaxLength(CheckResultRecord.TargetMaxLength)
            .IsRequired();

        builder.Property(e => e.Succeeded)
            .HasColumnName("succeeded")
            .IsRequired();

        builder.Property(e => e.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.StatusCode)
            .HasColumnName("status_code");

        builder.Property(e => e.ElapsedMs)
            .HasColumnName("elapsed_ms")
            .IsRequired();

        builder.Property(e => e.CheckedAtUtc)
            .HasColumnName("checked_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.Message)
            .HasColumnName("message")
            .HasMaxLength(CheckResultRecord.MessageMaxLength)
            .IsRequired();

        builder.Property(e => e.ErrorType)
            .HasColumnName("error_type")
            .HasMaxLength(CheckResultRecord.ErrorTypeMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Foreign key to monitored_entities — declared here without a
        // navigation property on either side so loading a MonitoredEntity
        // never accidentally hydrates its history. ON DELETE CASCADE so
        // removing a monitored entity removes its history; that matches
        // the operator's mental model (the entity is gone, its results
        // are no longer meaningful) and prevents orphan rows.
        builder.HasOne<MonitoredEntity>()
            .WithMany()
            .HasForeignKey(e => e.MonitoredEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.MonitoredEntityId)
            .HasDatabaseName("ix_check_results_monitored_entity_id");

        builder.HasIndex(e => e.CheckedAtUtc)
            .HasDatabaseName("ix_check_results_checked_at_utc");

        builder.HasIndex(e => e.Outcome)
            .HasDatabaseName("ix_check_results_outcome");
    }
}
