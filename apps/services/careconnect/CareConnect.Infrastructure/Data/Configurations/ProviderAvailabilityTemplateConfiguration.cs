using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderAvailabilityTemplateConfiguration : IEntityTypeConfiguration<ProviderAvailabilityTemplate>
{
    public void Configure(EntityTypeBuilder<ProviderAvailabilityTemplate> builder)
    {
        builder.ToTable("cc_ProviderAvailabilityTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.ProviderId).IsRequired();
        builder.Property(t => t.FacilityId).IsRequired();
        builder.Property(t => t.ServiceOfferingId);
        builder.Property(t => t.DayOfWeek).IsRequired();
        builder.Property(t => t.StartTimeLocal).IsRequired().HasColumnType("time(0)");
        builder.Property(t => t.EndTimeLocal).IsRequired().HasColumnType("time(0)");
        builder.Property(t => t.SlotDurationMinutes).IsRequired();
        builder.Property(t => t.Capacity).IsRequired();
        builder.Property(t => t.EffectiveFrom);
        builder.Property(t => t.EffectiveTo);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.CreatedByUserId);
        builder.Property(t => t.UpdatedByUserId);

        builder.HasIndex(t => new { t.TenantId, t.ProviderId, t.DayOfWeek });
        builder.HasIndex(t => new { t.TenantId, t.FacilityId });
        builder.HasIndex(t => new { t.TenantId, t.ServiceOfferingId });

        builder.HasOne(t => t.Provider)
               .WithMany()
               .HasForeignKey(t => t.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Facility)
               .WithMany()
               .HasForeignKey(t => t.FacilityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ServiceOffering)
               .WithMany()
               .HasForeignKey(t => t.ServiceOfferingId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
