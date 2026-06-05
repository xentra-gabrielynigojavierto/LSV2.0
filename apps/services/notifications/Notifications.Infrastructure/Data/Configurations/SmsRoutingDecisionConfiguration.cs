using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsRoutingDecisionConfiguration : IEntityTypeConfiguration<SmsRoutingDecision>
{
    public void Configure(EntityTypeBuilder<SmsRoutingDecision> b)
    {
        b.ToTable("ntf_SmsRoutingDecisions");
        b.HasKey(d => d.Id);

        b.Property(d => d.Id).HasColumnType("char(36)");
        b.Property(d => d.TenantId).HasColumnType("char(36)");
        b.Property(d => d.NotificationId).HasColumnType("char(36)");
        b.Property(d => d.AttemptId).HasColumnType("char(36)");
        b.Property(d => d.RoutingPolicyId).HasColumnType("char(36)");
        b.Property(d => d.RoutingMode).IsRequired().HasMaxLength(30);
        b.Property(d => d.SelectedProvider).IsRequired().HasMaxLength(100);
        b.Property(d => d.SelectedProviderConfigId).HasColumnType("char(36)");
        b.Property(d => d.ProviderOwnershipMode).HasMaxLength(30);
        b.Property(d => d.CandidateProvidersJson).HasColumnType("text");
        b.Property(d => d.ExcludedProvidersJson).HasColumnType("text");
        b.Property(d => d.DecisionReason).IsRequired().HasMaxLength(500);
        b.Property(d => d.EstimatedCostAmount).HasColumnType("decimal(18,8)");
        b.Property(d => d.CostCurrency).HasMaxLength(3);
        b.Property(d => d.HealthSnapshotJson).HasColumnType("text");
        b.Property(d => d.Region).HasMaxLength(50);
        b.Property(d => d.CountryCode).HasMaxLength(10);
        // LS-NOTIF-SMS-015: Adaptive routing metadata
        b.Property(d => d.InferredCountryCode).HasMaxLength(10);
        b.Property(d => d.InferredRegion).HasMaxLength(50);
        b.Property(d => d.ProviderQualityScore).HasColumnType("decimal(5,2)");
        b.Property(d => d.AdaptiveScore).HasColumnType("decimal(5,2)");
        b.Property(d => d.AdaptiveInputsJson).HasColumnType("text");
        b.Property(d => d.CreatedAt).IsRequired();

        b.HasIndex(d => new { d.TenantId, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsRoutingDecisions_Tenant_CreatedAt");
        b.HasIndex(d => d.NotificationId)
            .HasDatabaseName("IX_ntf_SmsRoutingDecisions_NotificationId");
        b.HasIndex(d => d.RoutingPolicyId)
            .HasDatabaseName("IX_ntf_SmsRoutingDecisions_PolicyId");
    }
}
