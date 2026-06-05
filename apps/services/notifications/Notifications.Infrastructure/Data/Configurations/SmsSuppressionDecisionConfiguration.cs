using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsSuppressionDecisionConfiguration : IEntityTypeConfiguration<SmsSuppressionDecision>
{
    public void Configure(EntityTypeBuilder<SmsSuppressionDecision> b)
    {
        b.ToTable("ntf_SmsSuppressionDecisions");
        b.HasKey(d => d.Id);

        b.Property(d => d.Id).HasColumnType("char(36)");
        b.Property(d => d.RecipientHash).IsRequired().HasMaxLength(64);
        b.Property(d => d.TenantId).HasColumnType("char(36)");
        b.Property(d => d.NotificationId).HasColumnType("char(36)");
        b.Property(d => d.AttemptId).HasColumnType("char(36)");
        b.Property(d => d.DecisionType).IsRequired().HasMaxLength(30);
        b.Property(d => d.ReasonCode).IsRequired().HasMaxLength(50);
        b.Property(d => d.RiskScore).HasColumnType("decimal(5,2)");
        b.Property(d => d.QualityScore).HasColumnType("decimal(5,2)");
        b.Property(d => d.ProviderType).HasMaxLength(100);
        b.Property(d => d.CountryCode).HasMaxLength(10);
        b.Property(d => d.Region).HasMaxLength(50);
        b.Property(d => d.DecisionMetadataJson).HasColumnType("text");

        b.HasIndex(d => d.RecipientHash)
            .HasDatabaseName("IX_ntf_SmsSuppressionDecisions_Hash");
        b.HasIndex(d => new { d.TenantId, d.RecipientHash })
            .HasDatabaseName("IX_ntf_SmsSuppressionDecisions_Tenant_Hash");
        b.HasIndex(d => new { d.TenantId, d.DecisionType, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsSuppressionDecisions_Tenant_Type_Dt");
    }
}
