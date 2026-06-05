using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class AppointmentNoteConfiguration : IEntityTypeConfiguration<AppointmentNote>
{
    public void Configure(EntityTypeBuilder<AppointmentNote> builder)
    {
        builder.ToTable("cc_AppointmentNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();
        builder.Property(n => n.AppointmentId).IsRequired();
        builder.Property(n => n.NoteType).IsRequired().HasMaxLength(20);
        builder.Property(n => n.Content).IsRequired().HasMaxLength(4000);
        builder.Property(n => n.IsInternal).IsRequired();
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc).IsRequired();
        builder.Property(n => n.CreatedByUserId);
        builder.Property(n => n.UpdatedByUserId);

        builder.HasIndex(n => new { n.TenantId, n.AppointmentId, n.CreatedAtUtc });
        builder.HasIndex(n => new { n.TenantId, n.NoteType });

        builder.HasOne(n => n.Appointment)
               .WithMany()
               .HasForeignKey(n => n.AppointmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
