using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportExecutionConfiguration : IEntityTypeConfiguration<ReportExecution>
{
    public void Configure(EntityTypeBuilder<ReportExecution> builder)
    {
        builder.ToTable("rpt_ReportExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.TemplateVersionNumber)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(e => e.OutputDocumentId)
            .HasMaxLength(200);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(2000);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.ReportTemplateId)
            .HasColumnName("ReportDefinitionId");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.TenantId, e.CreatedAtUtc });

        builder.HasOne(e => e.ReportTemplate)
            .WithMany()
            .HasForeignKey(e => e.ReportTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
