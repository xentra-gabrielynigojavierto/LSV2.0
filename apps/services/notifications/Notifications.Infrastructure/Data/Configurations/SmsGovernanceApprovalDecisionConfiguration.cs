using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceApprovalDecisionConfiguration : IEntityTypeConfiguration<SmsGovernanceApprovalDecision>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceApprovalDecision> b)
    {
        b.ToTable("ntf_SmsGovernanceApprovalDecisions");
        b.HasKey(d => d.Id);

        b.Property(d => d.Id).HasColumnType("char(36)");
        b.Property(d => d.ApprovalRequestId).IsRequired().HasColumnType("char(36)");
        b.Property(d => d.ReleasePackageId).IsRequired().HasColumnType("char(36)");
        b.Property(d => d.Decision).IsRequired().HasMaxLength(20);
        b.Property(d => d.DecisionReason).HasMaxLength(1000);
        b.Property(d => d.DecidedBy).HasMaxLength(200);
        b.Property(d => d.DecidedByRole).HasMaxLength(100);

        // Decisions for a specific approval request (chronological)
        b.HasIndex(d => new { d.ApprovalRequestId, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovApprDecs_Request_Created");

        // All decisions for a release (audit)
        b.HasIndex(d => new { d.ReleasePackageId, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovApprDecs_Package_Created");
    }
}
