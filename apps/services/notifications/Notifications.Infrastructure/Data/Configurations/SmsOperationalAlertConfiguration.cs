using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsOperationalAlertConfiguration : IEntityTypeConfiguration<SmsOperationalAlert>
{
    public void Configure(EntityTypeBuilder<SmsOperationalAlert> builder)
    {
        builder.ToTable("ntf_SmsOperationalAlerts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AlertType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("warning").IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(100);
        builder.Property(e => e.Message).HasColumnType("text").IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active").IsRequired();
        builder.Property(e => e.OccurrenceCount).HasDefaultValue(1);
        builder.Property(e => e.MetricValue).HasColumnType("decimal(18,6)");
        builder.Property(e => e.ThresholdValue).HasColumnType("decimal(18,6)");
        builder.Property(e => e.ResolvedBy).HasMaxLength(255);
        builder.Property(e => e.ResolutionNote).HasColumnType("text");

        // ── Indexes for common filter patterns ────────────────────────────────

        // Primary operational query: active alerts, newest first.
        builder.HasIndex(e => new { e.Status, e.LastObservedAt })
            .HasDatabaseName("IX_SmsOperationalAlerts_Status_LastObservedAt");

        // Deduplication lookup: find active alert for a given rule + scope.
        builder.HasIndex(e => new { e.AlertType, e.Status, e.TenantId, e.Provider, e.ProviderConfigId })
            .HasDatabaseName("IX_SmsOperationalAlerts_AlertType_Status_Scope");

        // Tenant-scoped view.
        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_SmsOperationalAlerts_TenantId_Status_CreatedAt");
    }
}
