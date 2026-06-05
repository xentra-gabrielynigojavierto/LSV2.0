using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportTemplateVersionConfiguration : IEntityTypeConfiguration<ReportTemplateVersion>
{
    public void Configure(EntityTypeBuilder<ReportTemplateVersion> builder)
    {
        builder.ToTable("rpt_ReportTemplateVersions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.VersionNumber)
            .IsRequired();

        builder.Property(e => e.TemplateBody)
            .HasColumnType("longtext");

        builder.Property(e => e.OutputFormat)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ChangeNotes)
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.PublishedAtUtc);

        builder.Property(e => e.PublishedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.ReportTemplateId)
            .HasColumnName("ReportDefinitionId");

        builder.HasIndex(e => new { e.ReportTemplateId, e.VersionNumber }).IsUnique();
        builder.HasIndex(e => e.IsActive);
    }
}
