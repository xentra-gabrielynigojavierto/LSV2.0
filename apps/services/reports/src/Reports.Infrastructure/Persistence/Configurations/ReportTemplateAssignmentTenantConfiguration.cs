using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportTemplateAssignmentTenantConfiguration : IEntityTypeConfiguration<ReportTemplateAssignmentTenant>
{
    public void Configure(EntityTypeBuilder<ReportTemplateAssignmentTenant> builder)
    {
        builder.ToTable("rpt_ReportTemplateAssignmentTenants");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReportTemplateAssignmentId)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.ReportTemplateAssignmentId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.ReportTemplateAssignmentId, e.TenantId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
    }
}
