using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienCaseNoteConfiguration : IEntityTypeConfiguration<LienCaseNote>
{
    public void Configure(EntityTypeBuilder<LienCaseNote> builder)
    {
        builder.ToTable("liens_CaseNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.CaseId).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(n => n.Category)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("general");

        builder.Property(n => n.IsPinned)
            .IsRequired()
            .HasDefaultValue(false);

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

        builder.HasIndex(n => new { n.TenantId, n.CaseId, n.CreatedAtUtc })
            .HasDatabaseName("IX_CaseNotes_TenantId_CaseId_CreatedAt");

        builder.HasIndex(n => new { n.CaseId, n.IsDeleted })
            .HasDatabaseName("IX_CaseNotes_CaseId_IsDeleted");
    }
}
