using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core Fluent API mapping for the legacy <see cref="AuditEvent"/> model.
///
/// This configuration is extracted verbatim from the original inline OnModelCreating
/// block to keep DbContext clean while preserving backward compatibility.
/// The <see cref="AuditEvent"/> entity backs the existing service layer (InMemory provider)
/// and will be superseded by the <c>AuditEventRecord</c> entity when the service layer is
/// re-wired to the new entity model.
/// </summary>
internal sealed class LegacyAuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> entity)
    {
        entity.ToTable("aud_AuditEvents");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .IsRequired()
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        entity.Property(e => e.Source)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.Severity)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("INFO");

        entity.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(2000);

        entity.Property(e => e.Outcome)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("SUCCESS");

        entity.Property(e => e.TenantId).HasMaxLength(100);
        entity.Property(e => e.ActorId).HasMaxLength(200);
        entity.Property(e => e.ActorLabel).HasMaxLength(300);
        entity.Property(e => e.TargetType).HasMaxLength(200);
        entity.Property(e => e.TargetId).HasMaxLength(200);
        entity.Property(e => e.IpAddress).HasMaxLength(45);
        entity.Property(e => e.UserAgent).HasMaxLength(500);
        entity.Property(e => e.CorrelationId).HasMaxLength(200);
        entity.Property(e => e.Metadata).HasColumnType("text");
        entity.Property(e => e.IntegrityHash).HasMaxLength(64);
        entity.Property(e => e.OccurredAtUtc).IsRequired();
        entity.Property(e => e.IngestedAtUtc).IsRequired();

        entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEvents_TenantId_OccurredAt");
        entity.HasIndex(e => new { e.Source, e.EventType })
            .HasDatabaseName("IX_AuditEvents_Source_EventType");
        entity.HasIndex(e => new { e.Category, e.Severity, e.Outcome })
            .HasDatabaseName("IX_AuditEvents_Category_Severity_Outcome");
        entity.HasIndex(e => e.ActorId)
            .HasDatabaseName("IX_AuditEvents_ActorId");
        entity.HasIndex(e => new { e.TargetType, e.TargetId })
            .HasDatabaseName("IX_AuditEvents_TargetType_TargetId");
        entity.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_AuditEvents_CorrelationId");
        entity.HasIndex(e => e.IngestedAtUtc)
            .HasDatabaseName("IX_AuditEvents_IngestedAt");
    }
}
