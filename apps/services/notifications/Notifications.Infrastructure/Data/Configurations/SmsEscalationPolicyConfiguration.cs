using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsEscalationPolicyConfiguration : IEntityTypeConfiguration<SmsOperationalEscalationPolicy>
{
    public void Configure(EntityTypeBuilder<SmsOperationalEscalationPolicy> builder)
    {
        builder.ToTable("ntf_SmsEscalationPolicies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Enabled).HasDefaultValue(true);
        builder.Property(e => e.AlertType).HasMaxLength(100);
        builder.Property(e => e.Severity).HasMaxLength(20);
        builder.Property(e => e.Provider).HasMaxLength(100);
        builder.Property(e => e.ChannelType).HasMaxLength(50).IsRequired();

        // Target stores the raw webhook URL or email — TEXT, not exposed in API responses.
        builder.Property(e => e.Target).HasColumnType("text").IsRequired();
        builder.Property(e => e.TargetDisplay).HasMaxLength(500);

        builder.Property(e => e.CooldownMinutes).HasDefaultValue(60);
        builder.Property(e => e.MaxRetryCount).HasDefaultValue(3);
        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.UpdatedBy).HasMaxLength(255);

        // ── Indexes ───────────────────────────────────────────────────────────

        // Primary enabled-policy lookup: fetch active policies by alert type and severity.
        builder.HasIndex(e => new { e.Enabled, e.AlertType })
            .HasDatabaseName("IX_SmsEscalationPolicies_Enabled_AlertType");

        builder.HasIndex(e => new { e.Enabled, e.ChannelType })
            .HasDatabaseName("IX_SmsEscalationPolicies_Enabled_ChannelType");
    }
}
