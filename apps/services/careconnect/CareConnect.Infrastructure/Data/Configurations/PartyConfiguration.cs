using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("cc_Parties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.OwnerOrganizationId).IsRequired();
        builder.Property(p => p.PartyType).IsRequired().HasMaxLength(20);
        builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.LastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.MiddleName).HasMaxLength(100);
        builder.Property(p => p.PreferredName).HasMaxLength(100);
        builder.Property(p => p.DateOfBirth).HasColumnType("date");
        builder.Property(p => p.SsnLast4).HasMaxLength(4);
        builder.Property(p => p.LinkedUserId);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedByUserId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.HasMany(p => p.Contacts)
               .WithOne(c => c.Party)
               .HasForeignKey(c => c.PartyId)
               .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(p => new { p.TenantId, p.LastName, p.FirstName })
            .HasDatabaseName("IX_Parties_TenantId_Name");
        builder.HasIndex(p => p.OwnerOrganizationId)
            .HasDatabaseName("IX_Parties_OwnerOrganizationId");
        builder.HasIndex(p => new { p.TenantId, p.LinkedUserId })
            .HasDatabaseName("IX_Parties_TenantId_LinkedUserId");
        builder.HasIndex(p => new { p.TenantId, p.DateOfBirth, p.LastName })
            .HasDatabaseName("IX_Parties_TenantId_Dob_Name");
    }
}
