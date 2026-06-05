using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceRuleMatchMetricConfiguration : IEntityTypeConfiguration<SmsGovernanceRuleMatchMetric>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRuleMatchMetric> b)
    {
        b.ToTable("ntf_SmsGovernanceRuleMatchMetrics");
        b.HasKey(m => m.Id);

        b.Property(m => m.Id).HasColumnType("char(36)");
        b.Property(m => m.RuleId).HasColumnType("char(36)");
        b.Property(m => m.RulePackId).HasColumnType("char(36)");
        b.Property(m => m.TenantId).HasColumnType("char(36)");
        b.Property(m => m.RuleType).HasMaxLength(40);
        b.Property(m => m.Severity).HasMaxLength(20);
        b.Property(m => m.DecisionType).IsRequired().HasMaxLength(20);
        b.Property(m => m.ReasonCode).HasMaxLength(100);

        b.Property(m => m.MatchCount).HasDefaultValue(0);
        b.Property(m => m.BlockCount).HasDefaultValue(0);
        b.Property(m => m.WarnCount).HasDefaultValue(0);
        b.Property(m => m.ReviewCount).HasDefaultValue(0);
        b.Property(m => m.AllowCount).HasDefaultValue(0);
        b.Property(m => m.SimulationCount).HasDefaultValue(0);
        b.Property(m => m.LiveCount).HasDefaultValue(0);

        // Lookup by rule + tenant + window (analytics aggregation key)
        b.HasIndex(m => new { m.RuleId, m.TenantId, m.WindowStart })
            .HasDatabaseName("IX_ntf_SmsGovMatchMetrics_Rule_Tenant_Window");

        // Pack-level analytics
        b.HasIndex(m => new { m.RulePackId, m.WindowStart })
            .HasDatabaseName("IX_ntf_SmsGovMatchMetrics_Pack_Window");

        // Tenant breakdown
        b.HasIndex(m => new { m.TenantId, m.WindowStart })
            .HasDatabaseName("IX_ntf_SmsGovMatchMetrics_Tenant_Window");

        // Time-range queries
        b.HasIndex(m => m.WindowStart)
            .HasDatabaseName("IX_ntf_SmsGovMatchMetrics_WindowStart");
    }
}
