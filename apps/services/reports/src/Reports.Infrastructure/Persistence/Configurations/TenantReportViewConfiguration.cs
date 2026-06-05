using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class TenantReportViewConfiguration : IEntityTypeConfiguration<TenantReportView>
{
    public void Configure(EntityTypeBuilder<TenantReportView> builder)
    {
        builder.ToTable("rpt_TenantReportViews");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ReportTemplateId)
            .IsRequired();

        builder.Property(e => e.BaseTemplateVersionNumber)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.LayoutConfigJson)
            .HasColumnType("longtext");

        builder.Property(e => e.ColumnConfigJson)
            .HasColumnType("longtext");

        builder.Property(e => e.FilterConfigJson)
            .HasColumnType("longtext");

        builder.Property(e => e.FormulaConfigJson)
            .HasColumnType("longtext");

        builder.Property(e => e.FormattingConfigJson)
            .HasColumnType("longtext");

        builder.Property(e => e.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.ReportTemplateId);
        builder.HasIndex(e => new { e.TenantId, e.ReportTemplateId });
        builder.HasIndex(e => new { e.TenantId, e.ReportTemplateId, e.IsActive });

        builder.HasOne(e => e.ReportTemplate)
            .WithMany()
            .HasForeignKey(e => e.ReportTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
