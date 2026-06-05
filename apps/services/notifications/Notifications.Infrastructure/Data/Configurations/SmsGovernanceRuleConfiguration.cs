using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceRuleConfiguration : IEntityTypeConfiguration<SmsGovernanceRule>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRule> b)
    {
        b.ToTable("ntf_SmsGovernanceRules");
        b.HasKey(r => r.Id);

        b.Property(r => r.Id).HasColumnType("char(36)");
        b.Property(r => r.RulePackId).IsRequired().HasColumnType("char(36)");
        b.Property(r => r.Name).IsRequired().HasMaxLength(200);
        b.Property(r => r.Description).HasColumnType("text");
        b.Property(r => r.RuleType).IsRequired().HasMaxLength(40);
        b.Property(r => r.Pattern).HasMaxLength(500);
        b.Property(r => r.Severity).IsRequired().HasMaxLength(20).HasDefaultValue("block");
        b.Property(r => r.Enabled).HasDefaultValue(true);
        b.Property(r => r.Priority).HasDefaultValue(100);
        b.Property(r => r.MetadataJson).HasColumnType("text");
        b.Property(r => r.CreatedBy).HasMaxLength(200);
        b.Property(r => r.UpdatedBy).HasMaxLength(200);

        b.HasIndex(r => new { r.RulePackId, r.Enabled, r.Priority })
            .HasDatabaseName("IX_ntf_SmsGovRules_Pack_Enabled_Priority");

        b.HasIndex(r => new { r.RuleType, r.Enabled })
            .HasDatabaseName("IX_ntf_SmsGovRules_Type_Enabled");

        b.HasIndex(r => r.Severity)
            .HasDatabaseName("IX_ntf_SmsGovRules_Severity");
    }
}
