using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskNoteConfiguration : IEntityTypeConfiguration<LienTaskNote>
{
    public void Configure(EntityTypeBuilder<LienTaskNote> builder)
    {
        builder.ToTable("liens_TaskNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.TaskId).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(n => n.CreatedByUserId).IsRequired();

        builder.Property(n => n.CreatedByName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(n => n.IsEdited)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc);

        builder.HasIndex(n => new { n.TenantId, n.TaskId, n.CreatedAtUtc })
            .HasDatabaseName("IX_TaskNotes_TenantId_TaskId_CreatedAt");

        builder.HasIndex(n => new { n.TaskId, n.IsDeleted })
            .HasDatabaseName("IX_TaskNotes_TaskId_IsDeleted");
    }
}
