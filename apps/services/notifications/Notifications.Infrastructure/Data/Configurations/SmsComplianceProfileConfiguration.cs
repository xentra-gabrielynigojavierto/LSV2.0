using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsComplianceProfileConfiguration : IEntityTypeConfiguration<SmsComplianceProfile>
{
    public void Configure(EntityTypeBuilder<SmsComplianceProfile> b)
    {
        b.ToTable("ntf_SmsComplianceProfiles");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnType("char(36)");
        b.Property(p => p.TenantId).HasColumnType("char(36)");
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.Description).HasColumnType("text");
        b.Property(p => p.Enabled).HasDefaultValue(true);
        b.Property(p => p.DefaultRulePackIdsJson).HasColumnType("text");
        b.Property(p => p.EnforcementMode).IsRequired().HasMaxLength(20).HasDefaultValue("standard");
        b.Property(p => p.CreatedBy).HasMaxLength(200);
        b.Property(p => p.UpdatedBy).HasMaxLength(200);

        b.HasIndex(p => new { p.TenantId, p.Enabled })
            .HasDatabaseName("IX_ntf_SmsComplianceProfiles_Tenant_Enabled");

        b.HasIndex(p => p.EnforcementMode)
            .HasDatabaseName("IX_ntf_SmsComplianceProfiles_EnforcementMode");
    }
}
