using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class AppointmentAttachmentConfiguration : IEntityTypeConfiguration<AppointmentAttachment>
{
    public void Configure(EntityTypeBuilder<AppointmentAttachment> builder)
    {
        builder.ToTable("cc_AppointmentAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.AppointmentId).IsRequired();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.FileSizeBytes).IsRequired();
        builder.Property(a => a.ExternalDocumentId).HasMaxLength(500);
        builder.Property(a => a.ExternalStorageProvider).HasMaxLength(100);
        builder.Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        builder.HasIndex(a => new { a.TenantId, a.AppointmentId, a.CreatedAtUtc });
        builder.HasIndex(a => new { a.TenantId, a.Status });

        builder.HasOne(a => a.Appointment)
               .WithMany()
               .HasForeignKey(a => a.AppointmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
