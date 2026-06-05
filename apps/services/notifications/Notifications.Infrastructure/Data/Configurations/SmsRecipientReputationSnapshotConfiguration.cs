using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsRecipientReputationSnapshotConfiguration : IEntityTypeConfiguration<SmsRecipientReputationSnapshot>
{
    public void Configure(EntityTypeBuilder<SmsRecipientReputationSnapshot> b)
    {
        b.ToTable("ntf_SmsRecipientReputationSnapshots");
        b.HasKey(s => s.Id);

        b.Property(s => s.Id).HasColumnType("char(36)");
        b.Property(s => s.RecipientHash).IsRequired().HasMaxLength(64);
        b.Property(s => s.TenantId).HasColumnType("char(36)");
        b.Property(s => s.ProviderType).HasMaxLength(100);
        b.Property(s => s.CountryCode).HasMaxLength(10);
        b.Property(s => s.Region).HasMaxLength(50);

        b.Property(s => s.AverageLatencyMs).HasColumnType("decimal(10,2)");
        b.Property(s => s.DeliverySuccessRate).HasColumnType("decimal(5,4)");
        b.Property(s => s.FailureRate).HasColumnType("decimal(5,4)");
        b.Property(s => s.RetryRate).HasColumnType("decimal(5,4)");
        b.Property(s => s.DeadLetterRate).HasColumnType("decimal(5,4)");
        b.Property(s => s.CarrierFailureRate).HasColumnType("decimal(5,4)");
        b.Property(s => s.InvalidNumberRisk).HasColumnType("decimal(5,2)");
        b.Property(s => s.RetrySuppressionRisk).HasColumnType("decimal(5,2)");
        b.Property(s => s.QualityScore).HasColumnType("decimal(5,2)");
        b.Property(s => s.DestinationRiskLevel).IsRequired().HasMaxLength(20).HasDefaultValue("low");

        b.HasIndex(s => s.RecipientHash)
            .HasDatabaseName("IX_ntf_SmsRecipientSnapshots_Hash");
        b.HasIndex(s => new { s.TenantId, s.RecipientHash })
            .HasDatabaseName("IX_ntf_SmsRecipientSnapshots_Tenant_Hash");
        b.HasIndex(s => new { s.ProviderType, s.RecipientHash })
            .HasDatabaseName("IX_ntf_SmsRecipientSnapshots_Provider_Hash");
        b.HasIndex(s => new { s.CountryCode, s.CalculatedAt })
            .HasDatabaseName("IX_ntf_SmsRecipientSnapshots_Country_Calc");
        b.HasIndex(s => s.DestinationRiskLevel)
            .HasDatabaseName("IX_ntf_SmsRecipientSnapshots_RiskLevel");
    }
}
