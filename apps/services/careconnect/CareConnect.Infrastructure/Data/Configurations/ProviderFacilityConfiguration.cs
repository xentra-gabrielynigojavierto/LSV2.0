using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderFacilityConfiguration : IEntityTypeConfiguration<ProviderFacility>
{
    public void Configure(EntityTypeBuilder<ProviderFacility> builder)
    {
        builder.ToTable("cc_ProviderFacilities");

        builder.HasKey(pf => new { pf.ProviderId, pf.FacilityId });

        builder.Property(pf => pf.ProviderId).IsRequired();
        builder.Property(pf => pf.FacilityId).IsRequired();
        builder.Property(pf => pf.IsPrimary).IsRequired();

        builder.HasOne(pf => pf.Provider)
               .WithMany()
               .HasForeignKey(pf => pf.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
