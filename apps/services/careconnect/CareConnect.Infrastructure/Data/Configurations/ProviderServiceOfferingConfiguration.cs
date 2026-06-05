using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderServiceOfferingConfiguration : IEntityTypeConfiguration<ProviderServiceOffering>
{
    public void Configure(EntityTypeBuilder<ProviderServiceOffering> builder)
    {
        builder.ToTable("cc_ProviderServiceOfferings");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.ProviderId).IsRequired();
        builder.Property(p => p.ServiceOfferingId).IsRequired();
        builder.Property(p => p.FacilityId);
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasIndex(p => new { p.ProviderId, p.ServiceOfferingId, p.FacilityId }).IsUnique();

        builder.HasOne(p => p.Provider)
               .WithMany()
               .HasForeignKey(p => p.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ServiceOffering)
               .WithMany()
               .HasForeignKey(p => p.ServiceOfferingId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Facility)
               .WithMany()
               .HasForeignKey(p => p.FacilityId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
