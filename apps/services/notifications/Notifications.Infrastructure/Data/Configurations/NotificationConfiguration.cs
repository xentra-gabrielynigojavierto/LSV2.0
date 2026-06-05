using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("ntf_Notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("accepted");
        builder.Property(e => e.RecipientJson).HasColumnType("text");
        builder.Property(e => e.MessageJson).HasColumnType("text");
        builder.Property(e => e.MetadataJson).HasColumnType("text");
        builder.Property(e => e.IdempotencyKey).HasMaxLength(255);
        builder.Property(e => e.ProviderUsed).HasMaxLength(100);
        builder.Property(e => e.FailureCategory).HasMaxLength(100);
        builder.Property(e => e.LastErrorMessage).HasColumnType("text");
        builder.Property(e => e.TemplateKey).HasMaxLength(200);
        builder.Property(e => e.RenderedSubject).HasColumnType("text");
        builder.Property(e => e.RenderedBody).HasColumnType("text");
        builder.Property(e => e.RenderedText).HasColumnType("text");
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(50);
        builder.Property(e => e.PlatformFallbackUsed).HasDefaultValue(false);
        builder.Property(e => e.BlockedByPolicy).HasDefaultValue(false);
        builder.Property(e => e.BlockedReasonCode).HasMaxLength(100);
        builder.Property(e => e.OverrideUsed).HasDefaultValue(false);
        builder.Property(e => e.Severity).HasMaxLength(50);
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.RetryCount).HasDefaultValue(0);
        builder.Property(e => e.MaxRetries).HasDefaultValue(3);

        builder.HasIndex(e => new { e.Status, e.NextRetryAt })
            .HasDatabaseName("IX_Notifications_Status_NextRetryAt");

        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .HasDatabaseName("UX_Notifications_TenantId_IdempotencyKey")
            .IsUnique()
            .HasFilter("IdempotencyKey IS NOT NULL");
    }
}

public class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
    {
        builder.ToTable("ntf_NotificationAttempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Provider).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(e => e.AttemptNumber).HasDefaultValue(1);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(500);
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(50);
        builder.Property(e => e.FailureCategory).HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasColumnType("text");
        builder.Property(e => e.IsFailover).HasDefaultValue(false);

        // ── LS-NOTIF-SMS-007: Reconciliation tracking columns ──────────────────
        builder.Property(e => e.LastReconciliationOutcome).HasMaxLength(100);
        builder.Property(e => e.LastReconciliationErrorCode).HasMaxLength(100);
        builder.Property(e => e.LastReconciliationProviderStatus).HasMaxLength(100);
        builder.Property(e => e.LastReconciliationNormalizedStatus).HasMaxLength(100);
        builder.Property(e => e.ReconciliationAttemptCount).HasDefaultValue(0);

        // ── LS-NOTIF-SMS-013: SMS cost metadata columns ────────────────────────
        // All nullable — pre-existing rows remain valid.
        // Decimal(18,8) supports sub-cent SMS rates (e.g. $0.0075).
        // CostCurrency: ISO 4217, max 3 chars. CostSource: max 30 chars.
        builder.Property(e => e.EstimatedCostAmount).HasColumnType("decimal(18,8)");
        builder.Property(e => e.ActualCostAmount).HasColumnType("decimal(18,8)");
        builder.Property(e => e.CostCurrency).HasMaxLength(3);
        builder.Property(e => e.CostSource).HasMaxLength(30);
        // CostRecordedAt: datetime(6) nullable — no special config needed

        // Existing indexes
        builder.HasIndex(e => e.NotificationId).HasDatabaseName("IX_NotificationAttempts_NotificationId");
        builder.HasIndex(e => e.ProviderMessageId).HasDatabaseName("IX_NotificationAttempts_ProviderMessageId");
    }
}
