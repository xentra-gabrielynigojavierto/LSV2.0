using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsGovernanceDecisionConfiguration : IEntityTypeConfiguration<SmsGovernanceDecision>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceDecision> b)
    {
        b.ToTable("ntf_SmsGovernanceDecisions");
        b.HasKey(d => d.Id);

        b.Property(d => d.Id).HasColumnType("char(36)");
        b.Property(d => d.NotificationId).HasColumnType("char(36)");
        b.Property(d => d.AttemptId).HasColumnType("char(36)");
        b.Property(d => d.TenantId).HasColumnType("char(36)");
        b.Property(d => d.PolicyId).HasColumnType("char(36)");
        b.Property(d => d.ProviderConfigId).HasColumnType("char(36)");
        b.Property(d => d.PolicyType).IsRequired().HasMaxLength(50);
        b.Property(d => d.DecisionType).IsRequired().HasMaxLength(30).HasDefaultValue("allow");
        b.Property(d => d.ReasonCode).IsRequired().HasMaxLength(60);
        b.Property(d => d.ProviderType).HasMaxLength(100);
        b.Property(d => d.CountryCode).HasMaxLength(10);
        b.Property(d => d.Region).HasMaxLength(50);
        b.Property(d => d.DecisionMetadataJson).HasColumnType("text");

        b.HasIndex(d => new { d.TenantId, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovDecisions_Tenant_Dt");

        b.HasIndex(d => new { d.DecisionType, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovDecisions_DecisionType_Dt");

        b.HasIndex(d => new { d.PolicyType, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsGovDecisions_PolicyType_Dt");

        b.HasIndex(d => d.NotificationId)
            .HasDatabaseName("IX_ntf_SmsGovDecisions_NotifId");
    }
}
