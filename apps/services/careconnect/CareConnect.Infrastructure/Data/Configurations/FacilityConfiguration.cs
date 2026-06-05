using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("cc_Facilities");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).IsRequired();
        builder.Property(f => f.TenantId).IsRequired();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.AddressLine1).IsRequired().HasMaxLength(300);
        builder.Property(f => f.City).IsRequired().HasMaxLength(100);
        builder.Property(f => f.State).IsRequired().HasMaxLength(100);
        builder.Property(f => f.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(f => f.Phone).HasMaxLength(50);
        builder.Property(f => f.IsActive).IsRequired();
        builder.Property(f => f.CreatedAtUtc).IsRequired();
        builder.Property(f => f.UpdatedAtUtc).IsRequired();
        builder.Property(f => f.CreatedByUserId);
        builder.Property(f => f.UpdatedByUserId);

        // Phase 5: nullable FK to Identity Organization
        builder.Property(f => f.OrganizationId);
        builder.HasIndex(f => f.OrganizationId)
            .HasDatabaseName("IX_Facilities_OrganizationId");

        builder.HasIndex(f => new { f.TenantId, f.Name });

        builder.HasMany(f => f.ProviderFacilities)
               .WithOne(pf => pf.Facility)
               .HasForeignKey(pf => pf.FacilityId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
