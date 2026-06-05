using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> builder)
    {
        builder.ToTable("cc_AppointmentStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).IsRequired();
        builder.Property(h => h.AppointmentId).IsRequired();
        builder.Property(h => h.TenantId).IsRequired();
        builder.Property(h => h.OldStatus).IsRequired().HasMaxLength(20);
        builder.Property(h => h.NewStatus).IsRequired().HasMaxLength(20);
        builder.Property(h => h.ChangedByUserId);
        builder.Property(h => h.ChangedAtUtc).IsRequired();
        builder.Property(h => h.Notes).HasMaxLength(2000);

        builder.HasIndex(h => new { h.TenantId, h.AppointmentId });
        builder.HasIndex(h => new { h.TenantId, h.ChangedAtUtc });

        builder.HasOne(h => h.Appointment)
               .WithMany()
               .HasForeignKey(h => h.AppointmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
