using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("tenant_Domains");

        builder.HasKey(d => d.Id);

        // ── Tenant FK ─────────────────────────────────────────────────────────

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("IX_tenant_Domains_TenantId");

        // ── Host ──────────────────────────────────────────────────────────────

        builder.Property(d => d.Host)
            .IsRequired()
            .HasMaxLength(253);

        builder.HasIndex(d => d.Host)
            .HasDatabaseName("IX_tenant_Domains_Host");

        // ── Enums stored as strings ───────────────────────────────────────────

        builder.Property(d => d.DomainType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("IX_tenant_Domains_Status");

        // ── Flags ─────────────────────────────────────────────────────────────

        builder.Property(d => d.IsPrimary)
            .IsRequired();

        // ── Timestamps ────────────────────────────────────────────────────────

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        // ── Navigation ────────────────────────────────────────────────────────

        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
