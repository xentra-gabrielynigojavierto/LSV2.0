using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class OrganizationTypeConfiguration : IEntityTypeConfiguration<OrganizationType>
{
    public void Configure(EntityTypeBuilder<OrganizationType> builder)
    {
        builder.ToTable("idt_OrganizationTypes");

        builder.HasKey(ot => ot.Id);

        builder.Property(ot => ot.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ot => ot.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ot => ot.Description)
            .HasMaxLength(500);

        builder.Property(ot => ot.IsSystem).IsRequired();
        builder.Property(ot => ot.IsActive).IsRequired();
        builder.Property(ot => ot.CreatedAtUtc).IsRequired();

        builder.HasIndex(ot => ot.Code).IsUnique();

        // Seed the five built-in org types, mirroring the static OrgType constants
        builder.HasData(
            new { Id = SeedIds.OrgTypeInternal,  Code = "INTERNAL",   DisplayName = "Internal",    Description = (string?)"LegalSynq platform-internal organization", IsSystem = true,  IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.OrgTypeLawFirm,   Code = "LAW_FIRM",   DisplayName = "Law Firm",    Description = (string?)"Legal services organization that refers clients", IsSystem = true,  IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.OrgTypeProvider,  Code = "PROVIDER",   DisplayName = "Provider",    Description = (string?)"Healthcare or service provider that receives referrals", IsSystem = true,  IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.OrgTypeFunder,    Code = "FUNDER",     DisplayName = "Funder",      Description = (string?)"Organization that funds cases or applications", IsSystem = true,  IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.OrgTypeLienOwner, Code = "LIEN_OWNER", DisplayName = "Lien Owner",  Description = (string?)"Organization that purchases and services liens",  IsSystem = true,  IsActive = true, CreatedAtUtc = SeedIds.SeededAt }
        );
    }
}
