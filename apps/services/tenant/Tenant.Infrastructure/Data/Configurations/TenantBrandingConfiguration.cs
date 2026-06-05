using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_Brandings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.TenantId)
            .IsRequired();

        builder.HasIndex(b => b.TenantId)
            .IsUnique();

        builder.Property(b => b.BrandName)
            .HasMaxLength(300);

        // ── Asset references ──────────────────────────────────────────────────

        builder.Property(b => b.LogoDocumentId);
        builder.Property(b => b.LogoWhiteDocumentId);
        builder.Property(b => b.FaviconDocumentId);

        // ── Theme colours ─────────────────────────────────────────────────────

        builder.Property(b => b.PrimaryColor)
            .HasMaxLength(7);

        builder.Property(b => b.SecondaryColor)
            .HasMaxLength(7);

        builder.Property(b => b.AccentColor)
            .HasMaxLength(7);

        builder.Property(b => b.TextColor)
            .HasMaxLength(7);

        builder.Property(b => b.BackgroundColor)
            .HasMaxLength(7);

        // ── Overrides ─────────────────────────────────────────────────────────

        builder.Property(b => b.WebsiteUrlOverride)
            .HasMaxLength(500);

        builder.Property(b => b.SupportEmailOverride)
            .HasMaxLength(320);

        builder.Property(b => b.SupportPhoneOverride)
            .HasMaxLength(30);

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(b => b.CreatedAtUtc)
            .IsRequired();

        builder.Property(b => b.UpdatedAtUtc)
            .IsRequired();
    }
}
