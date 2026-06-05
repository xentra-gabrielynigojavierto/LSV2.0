using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("rpt_ReportSchedules");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OrganizationType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.FrequencyType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.FrequencyConfigJson)
            .HasMaxLength(500);

        builder.Property(e => e.Timezone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ExportFormat)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.DeliveryMethod)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.DeliveryConfigJson)
            .HasMaxLength(2000);

        builder.Property(e => e.ParametersJson)
            .HasMaxLength(4000);

        builder.Property(e => e.RequiredFeatureCode)
            .HasMaxLength(50);

        builder.Property(e => e.MinimumTierCode)
            .HasMaxLength(50);

        builder.Property(e => e.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.NextRunAtUtc);
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
        builder.HasIndex(e => new { e.IsActive, e.NextRunAtUtc });

        builder.HasOne(e => e.ReportTemplate)
            .WithMany()
            .HasForeignKey(e => e.ReportTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
