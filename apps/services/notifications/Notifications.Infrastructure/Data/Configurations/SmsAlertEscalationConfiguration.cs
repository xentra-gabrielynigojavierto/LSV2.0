using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsAlertEscalationConfiguration : IEntityTypeConfiguration<SmsOperationalAlertEscalation>
{
    public void Configure(EntityTypeBuilder<SmsOperationalAlertEscalation> builder)
    {
        builder.ToTable("ntf_SmsAlertEscalations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ChannelType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TargetMasked).HasMaxLength(500);
        builder.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("warning").IsRequired();
        builder.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("pending").IsRequired();
        builder.Property(e => e.AttemptCount).HasDefaultValue(0);
        builder.Property(e => e.FailureReason).HasColumnType("text");
        builder.Property(e => e.PayloadHash).HasMaxLength(64);
        builder.Property(e => e.MetadataJson).HasColumnType("text");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Lookup all escalations for a given alert.
        builder.HasIndex(e => e.AlertId)
            .HasDatabaseName("IX_SmsAlertEscalations_AlertId");

        // Retry worker: find due pending retries.
        builder.HasIndex(e => new { e.Status, e.NextRetryAt })
            .HasDatabaseName("IX_SmsAlertEscalations_Status_NextRetryAt");

        // Dedup check: alert + policy + hash within cooldown.
        builder.HasIndex(e => new { e.AlertId, e.PolicyId, e.PayloadHash })
            .HasDatabaseName("IX_SmsAlertEscalations_AlertId_PolicyId_PayloadHash");

        // Time-range queries.
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_SmsAlertEscalations_CreatedAt");
    }
}
