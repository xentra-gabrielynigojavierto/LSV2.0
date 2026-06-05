using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("liens_Facilities");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).IsRequired();
        builder.Property(f => f.TenantId).IsRequired();
        builder.Property(f => f.OrgId).IsRequired();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Code)
            .HasMaxLength(50);

        builder.Property(f => f.ExternalReference)
            .HasMaxLength(200);

        builder.Property(f => f.AddressLine1)
            .HasMaxLength(300);

        builder.Property(f => f.AddressLine2)
            .HasMaxLength(300);

        builder.Property(f => f.City)
            .HasMaxLength(100);

        builder.Property(f => f.State)
            .HasMaxLength(100);

        builder.Property(f => f.PostalCode)
            .HasMaxLength(20);

        builder.Property(f => f.Phone)
            .HasMaxLength(30);

        builder.Property(f => f.Email)
            .HasMaxLength(320);

        builder.Property(f => f.Fax)
            .HasMaxLength(30);

        builder.Property(f => f.IsActive)
            .IsRequired();

        builder.Property(f => f.OrganizationId);

        builder.Property(f => f.CreatedByUserId).IsRequired();
        builder.Property(f => f.UpdatedByUserId);
        builder.Property(f => f.CreatedAtUtc).IsRequired();
        builder.Property(f => f.UpdatedAtUtc).IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.OrgId, f.Name })
            .HasDatabaseName("IX_Facilities_TenantId_OrgId_Name");

        builder.HasIndex(f => new { f.TenantId, f.Code })
            .HasDatabaseName("IX_Facilities_TenantId_Code");

        builder.HasIndex(f => f.OrganizationId)
            .HasDatabaseName("IX_Facilities_OrganizationId");
    }
}
