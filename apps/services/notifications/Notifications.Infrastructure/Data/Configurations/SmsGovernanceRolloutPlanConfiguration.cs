using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public sealed class SmsGovernanceRolloutPlanConfiguration
    : IEntityTypeConfiguration<SmsGovernanceRolloutPlan>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRolloutPlan> b)
    {
        b.ToTable("ntf_SmsGovernanceRolloutPlans");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id)
            .HasColumnType("char(36)");

        b.Property(p => p.ReleasePackageId)
            .HasColumnType("char(36)")
            .IsRequired();

        b.Property(p => p.TenantId)
            .HasColumnType("char(36)");

        b.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(p => p.Description)
            .HasMaxLength(1000);

        b.Property(p => p.RolloutState)
            .HasMaxLength(50)
            .IsRequired();

        b.Property(p => p.RolloutStrategy)
            .HasMaxLength(50)
            .IsRequired();

        b.Property(p => p.RollbackThresholdJson)
            .HasMaxLength(2000);

        b.Property(p => p.FailureReason)
            .HasMaxLength(1000);

        b.Property(p => p.CreatedBy)
            .HasMaxLength(200);

        b.Property(p => p.UpdatedBy)
            .HasMaxLength(200);

        b.HasIndex(p => p.ReleasePackageId)
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutPlans_ReleasePackageId");

        b.HasIndex(p => new { p.TenantId, p.RolloutState, p.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutPlans_Tenant_State_Dt");

        b.HasIndex(p => new { p.RolloutState, p.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutPlans_State_Dt");

        b.HasIndex(p => new { p.RolloutStrategy, p.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutPlans_Strategy_Dt");
    }
}
