using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public sealed class GovernanceExecutionRecordConfiguration : IEntityTypeConfiguration<GovernanceExecutionRecord>
{
    public void Configure(EntityTypeBuilder<GovernanceExecutionRecord> b)
    {
        b.ToTable("ntf_GovernanceExecutionRecords");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnType("char(36)");

        b.Property(r => r.NotificationId).HasColumnType("char(36)");
        b.Property(r => r.AttemptId).HasColumnType("char(36)");
        b.Property(r => r.TenantId).HasColumnType("char(36)");

        b.Property(r => r.ChannelType)
            .IsRequired()
            .HasMaxLength(50);

        b.Property(r => r.DecisionType)
            .IsRequired()
            .HasMaxLength(50);

        b.Property(r => r.ReasonCode)
            .IsRequired()
            .HasMaxLength(100);

        b.Property(r => r.MatchedRuleIdsJson).HasMaxLength(2000);
        b.Property(r => r.MatchedRulePackIdsJson).HasMaxLength(2000);
        b.Property(r => r.AppliedOverlayIdsJson).HasMaxLength(2000);
        b.Property(r => r.ContentClassification).HasMaxLength(100);
        b.Property(r => r.TopologyResolutionStatus).HasMaxLength(50);
        b.Property(r => r.EngineStatus).HasMaxLength(50);
        b.Property(r => r.SafeMetadataJson).HasMaxLength(2000);

        b.Property(r => r.IsSimulation).HasColumnType("tinyint(1)");
        b.Property(r => r.CreatedAt).IsRequired();

        b.HasIndex(r => new { r.ChannelType, r.CreatedAt })
            .HasDatabaseName("IX_ntf_GovExecRecords_Channel_CreatedAt");

        b.HasIndex(r => new { r.TenantId, r.ChannelType, r.CreatedAt })
            .HasDatabaseName("IX_ntf_GovExecRecords_Tenant_Channel_CreatedAt");

        b.HasIndex(r => new { r.DecisionType, r.CreatedAt })
            .HasDatabaseName("IX_ntf_GovExecRecords_Decision_CreatedAt");

        b.HasIndex(r => r.NotificationId)
            .HasDatabaseName("IX_ntf_GovExecRecords_NotificationId");

        b.HasIndex(r => new { r.IsSimulation, r.CreatedAt })
            .HasDatabaseName("IX_ntf_GovExecRecords_Simulation_CreatedAt");
    }
}
