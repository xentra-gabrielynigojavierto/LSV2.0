using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsProviderQualitySnapshotConfiguration : IEntityTypeConfiguration<SmsProviderQualitySnapshot>
{
    public void Configure(EntityTypeBuilder<SmsProviderQualitySnapshot> b)
    {
        b.ToTable("ntf_SmsProviderQualitySnapshots");
        b.HasKey(s => s.Id);

        b.Property(s => s.Id).HasColumnType("char(36)");
        b.Property(s => s.ProviderType).IsRequired().HasMaxLength(100);
        b.Property(s => s.ProviderConfigId).HasColumnType("char(36)");
        b.Property(s => s.ProviderOwnershipMode).HasMaxLength(30);
        b.Property(s => s.TenantId).HasColumnType("char(36)");
        b.Property(s => s.Region).HasMaxLength(50);
        b.Property(s => s.CountryCode).HasMaxLength(10);

        b.Property(s => s.WindowStart).IsRequired();
        b.Property(s => s.WindowEnd).IsRequired();

        b.Property(s => s.TotalAttempts).IsRequired();
        b.Property(s => s.DeliveredAttempts).IsRequired();
        b.Property(s => s.FailedAttempts).IsRequired();
        b.Property(s => s.RetryAttempts).IsRequired();
        b.Property(s => s.DeadLetterAttempts).IsRequired();
        b.Property(s => s.ReconciledAttempts).IsRequired();
        b.Property(s => s.ReconciliationFailures).IsRequired();

        b.Property(s => s.AverageLatencyMs).HasColumnType("decimal(18,4)");

        b.Property(s => s.DeliverySuccessRate).HasColumnType("decimal(5,4)").IsRequired();
        b.Property(s => s.FailureRate).HasColumnType("decimal(5,4)").IsRequired();
        b.Property(s => s.RetryRate).HasColumnType("decimal(5,4)").IsRequired();
        b.Property(s => s.DeadLetterRate).HasColumnType("decimal(5,4)").IsRequired();
        b.Property(s => s.ReconciliationFailureRate).HasColumnType("decimal(5,4)").IsRequired();

        b.Property(s => s.AverageEffectiveCost).HasColumnType("decimal(18,8)");
        b.Property(s => s.CostPerDeliveredMessage).HasColumnType("decimal(18,8)");

        b.Property(s => s.QualityScore).HasColumnType("decimal(5,2)").IsRequired();
        b.Property(s => s.CostEfficiencyScore).HasColumnType("decimal(5,2)");
        b.Property(s => s.HealthPenalty).HasColumnType("decimal(5,4)").IsRequired();

        b.Property(s => s.CalculatedAt).IsRequired();

        // Indexes for common query patterns
        b.HasIndex(s => new { s.ProviderType, s.CalculatedAt })
            .HasDatabaseName("IX_ntf_SmsQualitySnapshots_Provider_Calc");
        b.HasIndex(s => new { s.TenantId, s.ProviderType, s.CalculatedAt })
            .HasDatabaseName("IX_ntf_SmsQualitySnapshots_Tenant_Provider_Calc");
        b.HasIndex(s => new { s.CountryCode, s.ProviderType, s.CalculatedAt })
            .HasDatabaseName("IX_ntf_SmsQualitySnapshots_Country_Provider_Calc");
        b.HasIndex(s => new { s.ProviderConfigId, s.CalculatedAt })
            .HasDatabaseName("IX_ntf_SmsQualitySnapshots_Config_Calc");
    }
}
