using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceApprovalRequestConfiguration : IEntityTypeConfiguration<SmsGovernanceApprovalRequest>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceApprovalRequest> b)
    {
        b.ToTable("ntf_SmsGovernanceApprovalRequests");
        b.HasKey(r => r.Id);

        b.Property(r => r.Id).HasColumnType("char(36)");
        b.Property(r => r.ReleasePackageId).IsRequired().HasColumnType("char(36)");
        b.Property(r => r.ApproverRole).IsRequired().HasMaxLength(100);
        b.Property(r => r.Status).IsRequired().HasMaxLength(20);

        // Release + stage order (multi-stage lookup)
        b.HasIndex(r => new { r.ReleasePackageId, r.ApprovalStage })
            .HasDatabaseName("IX_ntf_SmsGovApprReqs_Package_Stage");

        // Pending approvals queue
        b.HasIndex(r => new { r.Status, r.RequestedAt })
            .HasDatabaseName("IX_ntf_SmsGovApprReqs_Status_Requested");
    }
}
