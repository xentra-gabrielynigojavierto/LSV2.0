using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Domain.Tenant>
{
    public void Configure(EntityTypeBuilder<Domain.Tenant> builder)
    {
        builder.ToTable("tenant_Tenants");

        builder.HasKey(t => t.Id);

        // ── Core identity ─────────────────────────────────────────────────────

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.LegalName)
            .HasMaxLength(300);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Subdomain)
            .HasMaxLength(63);

        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasFilter("`Subdomain` IS NOT NULL");

        // ── BLK-TS-02: Provisioning state ────────────────────────────────────

        builder.Property(t => t.ProvisioningStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(TenantProvisioningStatus.Unknown);

        builder.Property(t => t.ProvisionedAtUtc);

        builder.Property(t => t.LastProvisioningError)
            .HasMaxLength(1000);

        // ── Logo refs (kept for Identity backward-compat) ─────────────────────

        builder.Property(t => t.LogoDocumentId);
        builder.Property(t => t.LogoWhiteDocumentId);

        // ── Profile metadata ──────────────────────────────────────────────────

        builder.Property(t => t.WebsiteUrl)
            .HasMaxLength(500);

        builder.Property(t => t.TimeZone)
            .HasMaxLength(100);

        builder.Property(t => t.Locale)
            .HasMaxLength(20);

        // ── Contact ───────────────────────────────────────────────────────────

        builder.Property(t => t.SupportEmail)
            .HasMaxLength(320);

        builder.Property(t => t.SupportPhone)
            .HasMaxLength(30);

        // ── Address ───────────────────────────────────────────────────────────

        builder.Property(t => t.AddressLine1)
            .HasMaxLength(200);

        builder.Property(t => t.AddressLine2)
            .HasMaxLength(200);

        builder.Property(t => t.City)
            .HasMaxLength(100);

        builder.Property(t => t.StateOrProvince)
            .HasMaxLength(100);

        builder.Property(t => t.PostalCode)
            .HasMaxLength(20);

        builder.Property(t => t.CountryCode)
            .HasMaxLength(2);

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired();

        // ── Navigation: owned branding ────────────────────────────────────────

        builder.HasOne(t => t.Branding)
            .WithOne(b => b.Tenant)
            .HasForeignKey<Domain.TenantBranding>(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
