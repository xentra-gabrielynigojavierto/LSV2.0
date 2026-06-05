using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportTemplateAssignmentConfiguration : IEntityTypeConfiguration<ReportTemplateAssignment>
{
    public void Configure(EntityTypeBuilder<ReportTemplateAssignment> builder)
    {
        builder.ToTable("rpt_ReportTemplateAssignments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReportTemplateId)
            .IsRequired();

        builder.Property(e => e.AssignmentScope)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OrganizationType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.RequiredFeatureCode)
            .HasMaxLength(100);

        builder.Property(e => e.MinimumTierCode)
            .HasMaxLength(50);

        builder.Property(e => e.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.ReportTemplateId);
        builder.HasIndex(e => new { e.ReportTemplateId, e.AssignmentScope, e.IsActive });
        builder.HasIndex(e => new { e.ProductCode, e.OrganizationType });
        builder.HasIndex(e => e.IsActive);

        builder.HasOne(e => e.ReportTemplate)
            .WithMany(t => t.Assignments)
            .HasForeignKey(e => e.ReportTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.TenantTargets)
            .WithOne(t => t.Assignment)
            .HasForeignKey(t => t.ReportTemplateAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
