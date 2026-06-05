using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderAvailabilityExceptionConfiguration : IEntityTypeConfiguration<ProviderAvailabilityException>
{
    public void Configure(EntityTypeBuilder<ProviderAvailabilityException> builder)
    {
        builder.ToTable("cc_ProviderAvailabilityExceptions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ProviderId).IsRequired();
        builder.Property(e => e.FacilityId);
        builder.Property(e => e.StartAtUtc).IsRequired();
        builder.Property(e => e.EndAtUtc).IsRequired();
        builder.Property(e => e.ExceptionType).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.ProviderId, e.StartAtUtc });
        builder.HasIndex(e => new { e.TenantId, e.FacilityId, e.StartAtUtc });
        builder.HasIndex(e => new { e.TenantId, e.StartAtUtc, e.EndAtUtc });

        builder.HasOne(e => e.Provider)
               .WithMany()
               .HasForeignKey(e => e.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Facility)
               .WithMany()
               .HasForeignKey(e => e.FacilityId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
