using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.ToTable("rpt_ReportDefinitions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OrganizationType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CurrentVersion)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.ProductCode);
        builder.HasIndex(e => e.OrganizationType);
        builder.HasIndex(e => new { e.ProductCode, e.OrganizationType });
        builder.HasIndex(e => e.IsActive);

        builder.HasMany(e => e.Versions)
            .WithOne(v => v.ReportTemplate)
            .HasForeignKey(v => v.ReportTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
