using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class SmsGovernanceTenantRulePackAssignmentConfiguration
    : IEntityTypeConfiguration<SmsGovernanceTenantRulePackAssignment>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceTenantRulePackAssignment> builder)
    {
        builder.ToTable("ntf_SmsGovernanceTenantRulePackAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.TenantId).IsRequired().HasColumnType("char(36)");
        builder.Property(x => x.RulePackId).IsRequired().HasColumnType("char(36)");

        builder.Property(x => x.AssignmentState)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.AssignmentMode)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.Priority).HasColumnType("int");

        builder.Property(x => x.EffectiveFrom).HasColumnType("datetime(6)");
        builder.Property(x => x.EffectiveTo).HasColumnType("datetime(6)");

        builder.Property(x => x.RolloutPlanId).HasColumnType("char(36)");
        builder.Property(x => x.RolloutStageId).HasColumnType("char(36)");
        builder.Property(x => x.ReleasePackageId).HasColumnType("char(36)");

        builder.Property(x => x.AssignedBy).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.DeactivationReason).HasMaxLength(1000).HasColumnType("varchar(1000)");

        builder.Property(x => x.ActivatedAt).HasColumnType("datetime(6)");
        builder.Property(x => x.DeactivatedAt).HasColumnType("datetime(6)");
        builder.Property(x => x.SupersededAt).HasColumnType("datetime(6)");

        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime(6)");

        builder.HasIndex(x => new { x.TenantId, x.AssignmentState, x.Priority })
               .HasDatabaseName("IX_ntf_SmsGovTenantAssign_Tenant_State_Priority");
        builder.HasIndex(x => new { x.TenantId, x.RulePackId })
               .HasDatabaseName("IX_ntf_SmsGovTenantAssign_Tenant_Pack");
        builder.HasIndex(x => new { x.RulePackId, x.AssignmentState })
               .HasDatabaseName("IX_ntf_SmsGovTenantAssign_Pack_State");
        builder.HasIndex(x => new { x.RolloutPlanId, x.AssignmentState })
               .HasDatabaseName("IX_ntf_SmsGovTenantAssign_Rollout_State");
        builder.HasIndex(x => new { x.EffectiveFrom, x.EffectiveTo })
               .HasDatabaseName("IX_ntf_SmsGovTenantAssign_EffectiveWindow");
    }
}
