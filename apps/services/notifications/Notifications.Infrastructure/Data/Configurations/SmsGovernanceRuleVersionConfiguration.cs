using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceRuleVersionConfiguration : IEntityTypeConfiguration<SmsGovernanceRuleVersion>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRuleVersion> b)
    {
        b.ToTable("ntf_SmsGovernanceRuleVersions");
        b.HasKey(v => v.Id);

        b.Property(v => v.Id).HasColumnType("char(36)");
        b.Property(v => v.RuleId).IsRequired().HasColumnType("char(36)");
        b.Property(v => v.RulePackId).HasColumnType("char(36)");
        b.Property(v => v.VersionNumber).IsRequired();
        b.Property(v => v.RuleSnapshotJson).IsRequired().HasColumnType("mediumtext");
        b.Property(v => v.ChangeType).IsRequired().HasMaxLength(20);
        b.Property(v => v.ChangeReason).HasMaxLength(500);
        b.Property(v => v.CreatedBy).HasMaxLength(200);

        // Primary lookup: rule history in version order
        b.HasIndex(v => new { v.RuleId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UIX_ntf_SmsGovRuleVersions_Rule_Version");

        // Pack-level history queries
        b.HasIndex(v => new { v.RulePackId, v.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovRuleVersions_Pack_CreatedAt");

        // Time-range queries
        b.HasIndex(v => v.CreatedAt)
            .HasDatabaseName("IX_ntf_SmsGovRuleVersions_CreatedAt");
    }
}
