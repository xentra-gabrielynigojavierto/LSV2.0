using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantProductEntitlementConfiguration : IEntityTypeConfiguration<TenantProductEntitlement>
{
    public void Configure(EntityTypeBuilder<TenantProductEntitlement> builder)
    {
        builder.ToTable("tenant_ProductEntitlements");

        builder.HasKey(e => e.Id);

        // ── Tenant FK ─────────────────────────────────────────────────────────

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_tenant_ProductEntitlements_TenantId");

        // ── Product identity ──────────────────────────────────────────────────

        builder.Property(e => e.ProductKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.TenantId, e.ProductKey })
            .HasDatabaseName("IX_tenant_ProductEntitlements_TenantId_ProductKey");

        builder.Property(e => e.ProductDisplayName)
            .HasMaxLength(300);

        // ── Flags ─────────────────────────────────────────────────────────────

        builder.Property(e => e.IsEnabled)
            .IsRequired();

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.IsDefault })
            .HasDatabaseName("IX_tenant_ProductEntitlements_TenantId_IsDefault");

        // ── Plan ──────────────────────────────────────────────────────────────

        builder.Property(e => e.PlanCode)
            .HasMaxLength(100);

        // ── Effective dates ───────────────────────────────────────────────────

        builder.Property(e => e.EffectiveFromUtc);
        builder.Property(e => e.EffectiveToUtc);

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        // ── Navigation ────────────────────────────────────────────────────────

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
