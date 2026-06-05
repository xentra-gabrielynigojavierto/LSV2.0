using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceRulePackVersionConfiguration : IEntityTypeConfiguration<SmsGovernanceRulePackVersion>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRulePackVersion> b)
    {
        b.ToTable("ntf_SmsGovernanceRulePackVersions");
        b.HasKey(v => v.Id);

        b.Property(v => v.Id).HasColumnType("char(36)");
        b.Property(v => v.RulePackId).IsRequired().HasColumnType("char(36)");
        b.Property(v => v.VersionNumber).IsRequired();
        b.Property(v => v.PackSnapshotJson).IsRequired().HasColumnType("mediumtext");
        b.Property(v => v.IncludedRulesSnapshotJson).HasColumnType("longtext");
        b.Property(v => v.ChangeType).IsRequired().HasMaxLength(20);
        b.Property(v => v.ChangeReason).HasMaxLength(500);
        b.Property(v => v.CreatedBy).HasMaxLength(200);

        // Primary lookup: pack history in version order
        b.HasIndex(v => new { v.RulePackId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UIX_ntf_SmsGovPackVersions_Pack_Version");

        // Time-range queries
        b.HasIndex(v => v.CreatedAt)
            .HasDatabaseName("IX_ntf_SmsGovPackVersions_CreatedAt");
    }
}
