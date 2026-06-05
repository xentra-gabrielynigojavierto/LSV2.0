using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceRulePackConfiguration : IEntityTypeConfiguration<SmsGovernanceRulePack>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceRulePack> b)
    {
        b.ToTable("ntf_SmsGovernanceRulePacks");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnType("char(36)");
        b.Property(p => p.TenantId).HasColumnType("char(36)");
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.Description).HasColumnType("text");
        b.Property(p => p.Version).HasDefaultValue(1);
        b.Property(p => p.Status).IsRequired().HasMaxLength(20).HasDefaultValue("draft");
        b.Property(p => p.Enabled).HasDefaultValue(true);
        b.Property(p => p.InheritanceMode).IsRequired().HasMaxLength(20).HasDefaultValue("merge");
        b.Property(p => p.Priority).HasDefaultValue(100);
        b.Property(p => p.CreatedBy).HasMaxLength(200);
        b.Property(p => p.UpdatedBy).HasMaxLength(200);

        b.HasIndex(p => new { p.TenantId, p.Enabled, p.Priority })
            .HasDatabaseName("IX_ntf_SmsGovRulePacks_Tenant_Enabled_Priority");

        b.HasIndex(p => new { p.Status, p.Enabled })
            .HasDatabaseName("IX_ntf_SmsGovRulePacks_Status_Enabled");

        b.HasIndex(p => new { p.EffectiveFrom, p.EffectiveTo })
            .HasDatabaseName("IX_ntf_SmsGovRulePacks_Effective");
    }
}
