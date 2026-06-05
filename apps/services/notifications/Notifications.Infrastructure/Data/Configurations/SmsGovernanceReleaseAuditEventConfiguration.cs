using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceReleaseAuditEventConfiguration : IEntityTypeConfiguration<SmsGovernanceReleaseAuditEvent>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceReleaseAuditEvent> b)
    {
        b.ToTable("ntf_SmsGovernanceReleaseAuditEvents");
        b.HasKey(e => e.Id);

        b.Property(e => e.Id).HasColumnType("char(36)");
        b.Property(e => e.ReleasePackageId).IsRequired().HasColumnType("char(36)");
        b.Property(e => e.EventType).IsRequired().HasMaxLength(40);
        b.Property(e => e.PreviousState).HasMaxLength(30);
        b.Property(e => e.NewState).HasMaxLength(30);
        b.Property(e => e.Actor).HasMaxLength(200);
        b.Property(e => e.Reason).HasMaxLength(1000);
        b.Property(e => e.MetadataJson).HasColumnType("mediumtext");

        // Primary audit trail query: all events for a release in order
        b.HasIndex(e => new { e.ReleasePackageId, e.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovRelAudit_Package_Created");

        // Cross-release event-type queries (e.g., all activation events)
        b.HasIndex(e => new { e.EventType, e.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovRelAudit_EventType_Created");
    }
}
