// LSCC-009: EF Core configuration for ActivationRequest.
using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ActivationRequestConfiguration : IEntityTypeConfiguration<ActivationRequest>
{
    public void Configure(EntityTypeBuilder<ActivationRequest> builder)
    {
        builder.ToTable("cc_ActivationRequests");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ReferralId).IsRequired();
        builder.Property(a => a.ProviderId).IsRequired();

        builder.Property(a => a.ProviderName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ProviderEmail).IsRequired().HasMaxLength(320);

        builder.Property(a => a.RequesterName).HasMaxLength(200);
        builder.Property(a => a.RequesterEmail).HasMaxLength(320);

        builder.Property(a => a.ClientName).HasMaxLength(250);
        builder.Property(a => a.ReferringFirmName).HasMaxLength(300);
        builder.Property(a => a.RequestedService).HasMaxLength(300);

        builder.Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.Property(a => a.ApprovedByUserId);
        builder.Property(a => a.ApprovedAtUtc);
        builder.Property(a => a.LinkedOrganizationId);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        // Queue: fetch pending requests newest-first
        builder.HasIndex(a => new { a.Status, a.CreatedAtUtc })
            .HasDatabaseName("IX_ActivationRequests_Status_CreatedAt");

        // BLK-PERF-01: Tenant-scoped admin queue + analytics filter on TenantId + Status + date.
        // The existing (Status, CreatedAtUtc) index does not include TenantId, causing tenant
        // admin activation queue reads to scan across all tenants before applying the tenant filter.
        builder.HasIndex(a => new { a.TenantId, a.Status, a.CreatedAtUtc })
            .HasDatabaseName("IX_ActivationRequests_TenantId_Status_CreatedAt");

        // Deduplication key: one active request per referral+provider pair
        builder.HasIndex(a => new { a.ReferralId, a.ProviderId })
            .HasDatabaseName("IX_ActivationRequests_ReferralId_ProviderId");

        // Navigation: soft FK to Provider (no cascade — provider may be deleted independently)
        builder.HasOne(a => a.Provider)
            .WithMany()
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Navigation: soft FK to Referral
        builder.HasOne(a => a.Referral)
            .WithMany()
            .HasForeignKey(a => a.ReferralId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
