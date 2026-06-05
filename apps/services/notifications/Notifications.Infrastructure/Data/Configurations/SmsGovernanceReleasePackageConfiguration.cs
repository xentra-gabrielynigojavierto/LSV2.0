using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceReleasePackageConfiguration : IEntityTypeConfiguration<SmsGovernanceReleasePackage>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceReleasePackage> b)
    {
        b.ToTable("ntf_SmsGovernanceReleasePackages");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnType("char(36)");
        b.Property(p => p.TenantId).HasColumnType("char(36)");
        b.Property(p => p.SupersededByReleaseId).HasColumnType("char(36)");
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.ReleaseState).IsRequired().HasMaxLength(30);
        b.Property(p => p.ReleaseType).IsRequired().HasMaxLength(30);
        b.Property(p => p.CreatedBy).HasMaxLength(200);
        b.Property(p => p.UpdatedBy).HasMaxLength(200);

        // ── LS-NOTIF-SMS-021-HARDENING: Activation lock ───────────────────────
        b.Property(p => p.ActivationLockId).HasColumnType("char(36)");
        b.Property(p => p.ActivationLockedBy).HasMaxLength(200);

        // ── LS-NOTIF-SMS-021-HARDENING: Retry tracking ────────────────────────
        b.Property(p => p.ActivationAttemptCount).HasDefaultValue(0);
        b.Property(p => p.LastActivationFailureReason).HasMaxLength(500);

        // Worker retry-window poll
        b.HasIndex(p => new { p.ReleaseState, p.NextActivationRetryAt })
            .HasDatabaseName("IX_ntf_SmsGovRelPkgs_State_RetryAt");

        // TenantId + state — common admin list query
        b.HasIndex(p => new { p.TenantId, p.ReleaseState, p.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovRelPkgs_Tenant_State_Created");

        // Scheduled activation worker poll
        b.HasIndex(p => new { p.ReleaseState, p.ScheduledActivationAt })
            .HasDatabaseName("IX_ntf_SmsGovRelPkgs_State_Scheduled");

        // Type + creation order
        b.HasIndex(p => new { p.ReleaseType, p.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovRelPkgs_Type_Created");

        b.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_ntf_SmsGovRelPkgs_Created");
    }
}
