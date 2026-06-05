using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="AuditAlert"/>.
///
/// Table: aud_AuditAlerts
///   - bigint AUTO_INCREMENT PK (internal surrogate, clustered)
///   - char(36) AlertId — unique public identifier
///   - char(64) Fingerprint — deterministic dedup key (SHA-256 hex)
///   - varchar RuleKey — anomaly rule that triggered
///   - varchar ScopeType — "Platform" or "Tenant"
///   - varchar TenantId — nullable for platform-wide alerts
///   - tinyint Status — AlertStatus enum (0=Open, 1=Acknowledged, 2=Resolved)
///   - varchar Severity — "High", "Medium", "Low"
///   - varchar Title / text Description — human-readable content
///   - text ContextJson — safe metric/context payload
///   - varchar DrillDownPath — relative investigation URL
///   - datetime(6) FirstDetectedAtUtc / LastDetectedAtUtc — detection timeline
///   - int DetectionCount — cumulative detections while active
///   - datetime(6) AcknowledgedAtUtc / ResolvedAtUtc — nullable lifecycle timestamps
///   - varchar AcknowledgedBy / ResolvedBy — nullable operator identity
///
/// Indexes:
///   IX_AuditAlerts_AlertId_Unique         — public API point lookup
///   IX_AuditAlerts_Fingerprint            — deduplication query
///   IX_AuditAlerts_Status                 — active alert dashboard queries
///   IX_AuditAlerts_TenantId_Status        — tenant-scoped active alert queries
///   IX_AuditAlerts_FirstDetectedAtUtc     — chronological ordering
/// </summary>
public sealed class AuditAlertConfiguration : IEntityTypeConfiguration<AuditAlert>
{
    public void Configure(EntityTypeBuilder<AuditAlert> builder)
    {
        builder.ToTable("aud_AuditAlerts");

        // ── Primary key ────────────────────────────────────────────────────────

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
               .ValueGeneratedOnAdd();

        // ── Public identifier ──────────────────────────────────────────────────

        builder.Property(a => a.AlertId)
               .IsRequired()
               .HasColumnType("char(36)");

        builder.HasIndex(a => a.AlertId)
               .IsUnique()
               .HasDatabaseName("IX_AuditAlerts_AlertId_Unique");

        // ── Fingerprint (dedup) ────────────────────────────────────────────────

        builder.Property(a => a.Fingerprint)
               .IsRequired()
               .HasColumnType("char(64)");

        builder.HasIndex(a => a.Fingerprint)
               .HasDatabaseName("IX_AuditAlerts_Fingerprint");

        // ── Detection identity ─────────────────────────────────────────────────

        builder.Property(a => a.RuleKey)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(a => a.ScopeType)
               .IsRequired()
               .HasMaxLength(20);

        builder.Property(a => a.TenantId)
               .HasMaxLength(256);

        // ── Classification ─────────────────────────────────────────────────────

        builder.Property(a => a.Severity)
               .IsRequired()
               .HasMaxLength(20);

        builder.Property(a => a.Status)
               .IsRequired()
               .HasConversion<byte>();

        builder.HasIndex(a => a.Status)
               .HasDatabaseName("IX_AuditAlerts_Status");

        builder.HasIndex(a => new { a.TenantId, a.Status })
               .HasDatabaseName("IX_AuditAlerts_TenantId_Status");

        // ── Human-readable content ─────────────────────────────────────────────

        builder.Property(a => a.Title)
               .IsRequired()
               .HasMaxLength(512);

        builder.Property(a => a.Description)
               .IsRequired()
               .HasColumnType("text");

        builder.Property(a => a.ContextJson)
               .HasColumnType("text");

        builder.Property(a => a.DrillDownPath)
               .HasMaxLength(1024);

        // ── Detection timeline ─────────────────────────────────────────────────

        builder.Property(a => a.FirstDetectedAtUtc)
               .IsRequired()
               .HasColumnType("datetime(6)");

        builder.HasIndex(a => a.FirstDetectedAtUtc)
               .HasDatabaseName("IX_AuditAlerts_FirstDetectedAtUtc");

        builder.Property(a => a.LastDetectedAtUtc)
               .IsRequired()
               .HasColumnType("datetime(6)");

        builder.Property(a => a.DetectionCount)
               .IsRequired()
               .HasDefaultValue(1);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        builder.Property(a => a.AcknowledgedAtUtc)
               .HasColumnType("datetime(6)");

        builder.Property(a => a.AcknowledgedBy)
               .HasMaxLength(256);

        builder.Property(a => a.ResolvedAtUtc)
               .HasColumnType("datetime(6)");

        builder.Property(a => a.ResolvedBy)
               .HasMaxLength(256);
    }
}
