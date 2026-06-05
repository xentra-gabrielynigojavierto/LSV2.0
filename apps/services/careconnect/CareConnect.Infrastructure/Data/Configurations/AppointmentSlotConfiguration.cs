using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.ToTable("cc_AppointmentSlots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.ProviderId).IsRequired();
        builder.Property(s => s.FacilityId).IsRequired();
        builder.Property(s => s.ServiceOfferingId);
        builder.Property(s => s.ProviderAvailabilityTemplateId);
        builder.Property(s => s.StartAtUtc).IsRequired();
        builder.Property(s => s.EndAtUtc).IsRequired();
        builder.Property(s => s.Capacity).IsRequired();
        builder.Property(s => s.ReservedCount).IsRequired();
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.CreatedByUserId);
        builder.Property(s => s.UpdatedByUserId);

        builder.HasIndex(s => new { s.TenantId, s.ProviderId, s.StartAtUtc });
        builder.HasIndex(s => new { s.TenantId, s.FacilityId, s.StartAtUtc });
        builder.HasIndex(s => new { s.TenantId, s.ServiceOfferingId, s.StartAtUtc });
        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => new { s.TenantId, s.ProviderId, s.ProviderAvailabilityTemplateId, s.StartAtUtc })
               .IsUnique();

        builder.HasOne(s => s.Provider)
               .WithMany()
               .HasForeignKey(s => s.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Facility)
               .WithMany()
               .HasForeignKey(s => s.FacilityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ServiceOffering)
               .WithMany()
               .HasForeignKey(s => s.ServiceOfferingId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.ProviderAvailabilityTemplate)
               .WithMany()
               .HasForeignKey(s => s.ProviderAvailabilityTemplateId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
