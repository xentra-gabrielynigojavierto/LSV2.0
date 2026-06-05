using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskNoteConfiguration : IEntityTypeConfiguration<TaskNote>
{
    public void Configure(EntityTypeBuilder<TaskNote> builder)
    {
        builder.ToTable("tasks_Notes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.TaskId).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();

        builder.Property(n => n.Note)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(n => n.AuthorName)
            .HasMaxLength(200);

        builder.Property(n => n.IsEdited)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedByUserId).IsRequired();
        builder.Property(n => n.UpdatedByUserId);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc).IsRequired();

        builder.HasIndex(n => new { n.TenantId, n.TaskId })
            .HasDatabaseName("IX_Notes_TenantId_TaskId");
    }
}
