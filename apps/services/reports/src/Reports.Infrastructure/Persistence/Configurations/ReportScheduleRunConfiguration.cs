using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence.Configurations;

public class ReportScheduleRunConfiguration : IEntityTypeConfiguration<ReportScheduleRun>
{
    public void Configure(EntityTypeBuilder<ReportScheduleRun> builder)
    {
        builder.ToTable("rpt_ReportScheduleRuns");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(2000);

        builder.Property(e => e.DeliveryResultJson)
            .HasMaxLength(4000);

        builder.Property(e => e.GeneratedFileName)
            .HasMaxLength(300);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ScheduledForUtc).IsRequired();

        builder.HasIndex(e => e.ReportScheduleId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ReportScheduleId, e.CreatedAtUtc });

        builder.HasOne(e => e.ReportSchedule)
            .WithMany(s => s.Runs)
            .HasForeignKey(e => e.ReportScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
