using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public sealed class SmsGovernanceRolloutAuditEventConfiguration
    : IEntityTypeConfiguration<SmsGovernanceRolloutAuditEvent>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRolloutAuditEvent> b)
    {
        b.ToTable("ntf_SmsGovernanceRolloutAuditEvents");
        b.HasKey(e => e.Id);

        b.Property(e => e.Id)
            .HasColumnType("char(36)");

        b.Property(e => e.RolloutPlanId)
            .HasColumnType("char(36)")
            .IsRequired();

        b.Property(e => e.StageId)
            .HasColumnType("char(36)");

        b.Property(e => e.TenantId)
            .HasColumnType("char(36)");

        b.Property(e => e.EventType)
            .HasMaxLength(100)
            .IsRequired();

        b.Property(e => e.PreviousState)
            .HasMaxLength(50);

        b.Property(e => e.NewState)
            .HasMaxLength(50);

        b.Property(e => e.Actor)
            .HasMaxLength(200);

        b.Property(e => e.Reason)
            .HasMaxLength(1000);

        b.Property(e => e.MetadataJson)
            .HasMaxLength(4000);

        b.HasIndex(e => new { e.RolloutPlanId, e.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutAuditEvents_PlanId_Dt");

        b.HasIndex(e => new { e.EventType, e.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutAuditEvents_EventType_Dt");

        b.HasIndex(e => new { e.StageId, e.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovernanceRolloutAuditEvents_StageId_Dt");
    }
}
