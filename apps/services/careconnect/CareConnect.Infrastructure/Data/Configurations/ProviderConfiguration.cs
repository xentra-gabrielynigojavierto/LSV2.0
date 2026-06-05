using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("cc_Providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.OrganizationName).HasMaxLength(300);
        builder.Property(p => p.Email).IsRequired().HasMaxLength(320);
        builder.Property(p => p.Phone).IsRequired().HasMaxLength(50);
        builder.Property(p => p.AddressLine1).IsRequired().HasMaxLength(300);
        builder.Property(p => p.City).IsRequired().HasMaxLength(100);
        builder.Property(p => p.State).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.AcceptingReferrals).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.CreatedByUserId);
        builder.Property(p => p.UpdatedByUserId);

        builder.Property(p => p.Latitude)
            .HasColumnType("decimal(10,7)");

        builder.Property(p => p.Longitude)
            .HasColumnType("decimal(10,7)");

        builder.Property(p => p.GeoPointSource)
            .HasMaxLength(20);

        builder.Property(p => p.GeoUpdatedAtUtc);

        // Phase 5: nullable FK to Identity Organization
        builder.Property(p => p.OrganizationId);
        builder.HasIndex(p => p.OrganizationId)
            .HasDatabaseName("IX_Providers_OrganizationId");

        // CC2-INT-B06-01: NPI — globally unique across the shared provider registry.
        // Null allowed (NPI not always known). When set, must be unique platform-wide.
        // MySQL 8.0 does not support partial/filtered indexes — no HasFilter here.
        // Uniqueness enforced at application layer (NetworkService.FindByNpiAsync).
        builder.Property(p => p.Npi).HasMaxLength(20);
        builder.HasIndex(p => p.Npi)
            .HasDatabaseName("IX_Providers_Npi");

        // CC2-INT-B06-02: Access-stage lifecycle fields
        builder.Property(p => p.AccessStage)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("URL");
        builder.Property(p => p.IdentityUserId);
        builder.Property(p => p.CommonPortalActivatedAtUtc);
        builder.Property(p => p.TenantProvisionedAtUtc);
        builder.HasIndex(p => p.AccessStage)
            .HasDatabaseName("IX_Providers_AccessStage");
        builder.HasIndex(p => p.IdentityUserId)
            .HasDatabaseName("IX_Providers_IdentityUserId");

        // BLK-CC-02: Onboarding recovery state
        builder.Property(p => p.PendingTenantId);
        builder.Property(p => p.PendingTenantCode).HasMaxLength(100);
        builder.Property(p => p.PendingTenantSubdomain).HasMaxLength(200);
        builder.Property(p => p.TenantOnboardingStatus)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue("None");
        builder.Property(p => p.LastOnboardingError).HasMaxLength(500);
        builder.Property(p => p.LastOnboardingAttemptAtUtc);
        builder.HasIndex(p => p.TenantOnboardingStatus)
            .HasDatabaseName("IX_Providers_TenantOnboardingStatus");
        builder.HasIndex(p => p.PendingTenantId)
            .HasDatabaseName("IX_Providers_PendingTenantId");

        builder.HasIndex(p => new { p.TenantId, p.Email }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => new { p.TenantId, p.City, p.State });
        builder.HasIndex(p => new { p.TenantId, p.Latitude, p.Longitude });

        builder.HasMany(p => p.ProviderCategories)
               .WithOne(pc => pc.Provider)
               .HasForeignKey(pc => pc.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
