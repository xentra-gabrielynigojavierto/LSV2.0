using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("cc_Appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ReferralId).IsRequired();

        // Multi-org participant columns (nullable; denormalized from Referral at create time)
        builder.Property(a => a.ReferringOrganizationId);
        builder.Property(a => a.ReceivingOrganizationId);
        builder.Property(a => a.SubjectPartyId);

        // Phase 5: relationship context (denormalized from Referral at create time)
        builder.Property(a => a.OrganizationRelationshipId);
        builder.HasIndex(a => a.OrganizationRelationshipId)
            .HasDatabaseName("IX_Appointments_OrganizationRelationshipId");

        builder.Property(a => a.ProviderId).IsRequired();
        builder.Property(a => a.FacilityId).IsRequired();
        builder.Property(a => a.ServiceOfferingId);
        builder.Property(a => a.AppointmentSlotId);
        builder.Property(a => a.ScheduledStartAtUtc).IsRequired();
        builder.Property(a => a.ScheduledEndAtUtc).IsRequired();
        builder.Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        builder.HasIndex(a => new { a.TenantId, a.ReferralId })
            .HasDatabaseName("IX_Appointments_TenantId_ReferralId");
        builder.HasIndex(a => new { a.TenantId, a.ProviderId, a.ScheduledStartAtUtc })
            .HasDatabaseName("IX_Appointments_TenantId_ProviderId_Start");
        builder.HasIndex(a => new { a.TenantId, a.AppointmentSlotId })
            .HasDatabaseName("IX_Appointments_TenantId_SlotId");
        builder.HasIndex(a => new { a.TenantId, a.Status })
            .HasDatabaseName("IX_Appointments_TenantId_Status");
        builder.HasIndex(a => new { a.ReceivingOrganizationId, a.Status })
            .HasDatabaseName("IX_Appointments_ReceivingOrgId_Status");
        builder.HasIndex(a => a.SubjectPartyId)
            .HasDatabaseName("IX_Appointments_SubjectPartyId");

        builder.HasOne(a => a.Referral)
               .WithMany()
               .HasForeignKey(a => a.ReferralId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Provider)
               .WithMany()
               .HasForeignKey(a => a.ProviderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Facility)
               .WithMany()
               .HasForeignKey(a => a.FacilityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ServiceOffering)
               .WithMany()
               .HasForeignKey(a => a.ServiceOfferingId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.AppointmentSlot)
               .WithMany()
               .HasForeignKey(a => a.AppointmentSlotId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
