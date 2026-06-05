using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantCapabilityConfiguration : IEntityTypeConfiguration<TenantCapability>
{
    public void Configure(EntityTypeBuilder<TenantCapability> builder)
    {
        builder.ToTable("tenant_Capabilities");

        builder.HasKey(c => c.Id);

        // ── Tenant FK ─────────────────────────────────────────────────────────

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_tenant_Capabilities_TenantId");

        // ── Product entitlement FK (nullable — null = tenant-global) ──────────

        builder.Property(c => c.ProductEntitlementId);

        builder.HasIndex(c => c.ProductEntitlementId)
            .HasDatabaseName("IX_tenant_Capabilities_ProductEntitlementId");

        builder.HasOne(c => c.ProductEntitlement)
            .WithMany()
            .HasForeignKey(c => c.ProductEntitlementId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // ── Capability key ────────────────────────────────────────────────────

        builder.Property(c => c.CapabilityKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(c => new { c.TenantId, c.CapabilityKey })
            .HasDatabaseName("IX_tenant_Capabilities_TenantId_CapabilityKey");

        // ── Flags ─────────────────────────────────────────────────────────────

        builder.Property(c => c.IsEnabled)
            .IsRequired();

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        // ── Navigation ────────────────────────────────────────────────────────

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
