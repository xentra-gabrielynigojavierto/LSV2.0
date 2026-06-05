using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernancePolicyConfiguration : IEntityTypeConfiguration<SmsGovernancePolicy>
{
    public void Configure(EntityTypeBuilder<SmsGovernancePolicy> b)
    {
        b.ToTable("ntf_SmsGovernancePolicies");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnType("char(36)");
        b.Property(p => p.TenantId).HasColumnType("char(36)");
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.PolicyType).IsRequired().HasMaxLength(50);
        b.Property(p => p.Enabled).HasDefaultValue(true);
        b.Property(p => p.Priority).HasDefaultValue(100);
        b.Property(p => p.PolicyJson).IsRequired().HasColumnType("text");
        b.Property(p => p.EmergencyOverrideAllowed).HasDefaultValue(false);
        b.Property(p => p.CreatedBy).HasMaxLength(200);
        b.Property(p => p.UpdatedBy).HasMaxLength(200);

        b.HasIndex(p => new { p.TenantId, p.PolicyType, p.Enabled })
            .HasDatabaseName("IX_ntf_SmsGovPolicies_Tenant_Type_Enabled");

        b.HasIndex(p => new { p.PolicyType, p.Enabled, p.Priority })
            .HasDatabaseName("IX_ntf_SmsGovPolicies_Type_Enabled_Priority");

        b.HasIndex(p => p.UpdatedAt)
            .HasDatabaseName("IX_ntf_SmsGovPolicies_UpdatedAt");
    }
}
