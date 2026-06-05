using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("idt_Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.IsSystemRole)
            .IsRequired();

        // PUM-B02-R03: scope classification (Platform | Tenant | Product)
        builder.Property(r => r.Scope)
            .HasMaxLength(20);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique();

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.Roles)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            // ── Platform Roles ───────────────────────────────────────────────
            new
            {
                Id = SeedIds.RolePlatformAdmin,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformAdmin",
                Description = (string?)"Full platform administration access",
                IsSystemRole = true,
                Scope = (string?)"Platform",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RolePlatformOps,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformOps",
                Description = (string?)"Platform operations — read access to all areas, limited management",
                IsSystemRole = true,
                Scope = (string?)"Platform",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RolePlatformSupport,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformSupport",
                Description = (string?)"Platform support — read access to users and tenants",
                IsSystemRole = true,
                Scope = (string?)"Platform",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RolePlatformBilling,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformBilling",
                Description = (string?)"Platform billing — manages product and entitlement configuration",
                IsSystemRole = true,
                Scope = (string?)"Platform",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RolePlatformAuditor,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformAuditor",
                Description = (string?)"Platform auditor — read-only access to audit logs and monitoring",
                IsSystemRole = true,
                Scope = (string?)"Platform",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            // ── Tenant Roles ─────────────────────────────────────────────────
            new
            {
                Id = SeedIds.RoleTenantAdmin,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "TenantAdmin",
                Description = (string?)"Tenant-level administration access",
                IsSystemRole = true,
                Scope = (string?)"Tenant",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleTenantManager,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "TenantManager",
                Description = (string?)"Tenant manager — manages users and settings within a tenant",
                IsSystemRole = true,
                Scope = (string?)"Tenant",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleTenantStaff,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "TenantStaff",
                Description = (string?)"Tenant staff — standard operational access within a tenant",
                IsSystemRole = true,
                Scope = (string?)"Tenant",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleTenantViewer,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "TenantViewer",
                Description = (string?)"Tenant viewer — read-only access within a tenant",
                IsSystemRole = true,
                Scope = (string?)"Tenant",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleStandardUser,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "StandardUser",
                Description = (string?)"Standard user access",
                IsSystemRole = true,
                Scope = (string?)"Tenant",
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            }
        );
    }
}
