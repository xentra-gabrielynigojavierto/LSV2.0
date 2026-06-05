using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsRoutingPolicyConfiguration : IEntityTypeConfiguration<SmsRoutingPolicy>
{
    public void Configure(EntityTypeBuilder<SmsRoutingPolicy> b)
    {
        b.ToTable("ntf_SmsRoutingPolicies");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnType("char(36)");
        b.Property(p => p.TenantId).HasColumnType("char(36)");
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.Enabled).IsRequired().HasDefaultValue(true);
        b.Property(p => p.Region).HasMaxLength(50);
        b.Property(p => p.CountryCode).HasMaxLength(10);
        b.Property(p => p.RoutingMode).IsRequired().HasMaxLength(30);
        b.Property(p => p.PreferredProvidersJson).HasColumnType("text");
        b.Property(p => p.ExcludedProvidersJson).HasColumnType("text");
        b.Property(p => p.MaxEstimatedCostPerMessage).HasColumnType("decimal(18,8)");
        b.Property(p => p.RequireHealthyProvider).IsRequired().HasDefaultValue(false);
        b.Property(p => p.FallbackToPlatform).IsRequired().HasDefaultValue(true);
        b.Property(p => p.Priority).IsRequired().HasDefaultValue(0);
        b.Property(p => p.CreatedAt).IsRequired();
        b.Property(p => p.UpdatedAt).IsRequired();
        b.Property(p => p.CreatedBy).HasMaxLength(255);
        b.Property(p => p.UpdatedBy).HasMaxLength(255);

        b.HasIndex(p => new { p.TenantId, p.Enabled, p.Priority })
            .HasDatabaseName("IX_ntf_SmsRoutingPolicies_Tenant_Enabled_Priority");
    }
}
