using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public sealed class SmsGovernanceTenantCohortConfiguration
    : IEntityTypeConfiguration<SmsGovernanceTenantCohort>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceTenantCohort> b)
    {
        b.ToTable("ntf_SmsGovernanceTenantCohorts");
        b.HasKey(c => c.Id);

        b.Property(c => c.Id)
            .HasColumnType("char(36)");

        b.Property(c => c.RolloutPlanId)
            .HasColumnType("char(36)")
            .IsRequired();

        b.Property(c => c.StageId)
            .HasColumnType("char(36)");

        b.Property(c => c.TenantId)
            .HasColumnType("char(36)")
            .IsRequired();

        b.Property(c => c.CohortName)
            .HasMaxLength(200)
            .IsRequired();

        // Prevent duplicate TenantId per (RolloutPlanId, StageId).
        // StageId null = plan-level cohort; unique per plan+tenant when stage is null.
        b.HasIndex(c => new { c.RolloutPlanId, c.TenantId })
            .HasDatabaseName("IX_ntf_SmsGovernanceTenantCohorts_PlanId_TenantId");

        b.HasIndex(c => new { c.StageId, c.TenantId })
            .HasDatabaseName("IX_ntf_SmsGovernanceTenantCohorts_StageId_TenantId");

        b.HasIndex(c => c.CohortName)
            .HasDatabaseName("IX_ntf_SmsGovernanceTenantCohorts_CohortName");
    }
}
