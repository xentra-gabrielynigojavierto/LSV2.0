using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public sealed class SmsGovernanceRolloutStageConfiguration
    : IEntityTypeConfiguration<SmsGovernanceRolloutStage>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRolloutStage> b)
    {
        b.ToTable("ntf_SmsGovernanceRolloutStages");
        b.HasKey(s => s.Id);

        b.Property(s => s.Id)
            .HasColumnType("char(36)");

        b.Property(s => s.RolloutPlanId)
            .HasColumnType("char(36)")
            .IsRequired();

        b.Property(s => s.StageName)
            .HasMaxLength(200);

        b.Property(s => s.StageState)
            .HasMaxLength(50)
            .IsRequired();

        b.Property(s => s.TenantPercentage)
            .HasPrecision(5, 2);

        b.Property(s => s.FailureReason)
            .HasMaxLength(500);

        b.HasIndex(s => new { s.RolloutPlanId, s.StageNumber })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutStages_PlanId_StageNum")
            .IsUnique();

        b.HasIndex(s => new { s.RolloutPlanId, s.StageState })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutStages_PlanId_State");
    }
}
