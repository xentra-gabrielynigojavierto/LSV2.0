using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TenantProviderConfigConfiguration : IEntityTypeConfiguration<TenantProviderConfig>
{
    public void Configure(EntityTypeBuilder<TenantProviderConfig> builder)
    {
        builder.ToTable("ntf_TenantProviderConfigs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.ProviderType).HasMaxLength(50);
        builder.Property(e => e.DisplayName).HasMaxLength(200);
        builder.Property(e => e.CredentialsJson).HasColumnType("text");
        builder.Property(e => e.SettingsJson).HasColumnType("text");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.ValidationStatus).HasMaxLength(30).HasDefaultValue("not_validated");
        builder.Property(e => e.ValidationMessage).HasColumnType("text");
        builder.Property(e => e.HealthStatus).HasMaxLength(20).HasDefaultValue("unknown");
        builder.Property(e => e.Priority).HasDefaultValue(1);

        builder.HasIndex(e => new { e.TenantId, e.Channel }).HasDatabaseName("IX_TenantProviderConfigs_TenantId_Channel");
    }
}

public class TenantChannelProviderSettingConfiguration : IEntityTypeConfiguration<TenantChannelProviderSetting>
{
    public void Configure(EntityTypeBuilder<TenantChannelProviderSetting> builder)
    {
        builder.ToTable("ntf_TenantChannelProviderSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.ProviderMode).HasMaxLength(30).HasDefaultValue("platform_managed");
        builder.Property(e => e.AllowPlatformFallback).HasDefaultValue(true);
        builder.Property(e => e.AllowAutomaticFailover).HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Channel })
            .HasDatabaseName("UX_TenantChannelProviderSettings_TenantId_Channel")
            .IsUnique();
    }
}

public class ProviderHealthConfiguration : IEntityTypeConfiguration<ProviderHealth>
{
    public void Configure(EntityTypeBuilder<ProviderHealth> builder)
    {
        builder.ToTable("ntf_ProviderHealth");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderType).HasMaxLength(50);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.OwnershipMode).HasMaxLength(20).HasDefaultValue("platform");
        builder.Property(e => e.HealthStatus).HasMaxLength(20).HasDefaultValue("healthy");
        builder.Property(e => e.ConsecutiveFailures).HasDefaultValue(0);
        builder.Property(e => e.ConsecutiveSuccesses).HasDefaultValue(0);
    }
}

public class ProviderWebhookLogConfiguration : IEntityTypeConfiguration<ProviderWebhookLog>
{
    public void Configure(EntityTypeBuilder<ProviderWebhookLog> builder)
    {
        builder.ToTable("ntf_ProviderWebhookLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(50);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.RequestHeadersJson).HasColumnType("text");
        builder.Property(e => e.PayloadJson).HasColumnType("longtext");
        builder.Property(e => e.ProcessingStatus).HasMaxLength(20).HasDefaultValue("received");
        builder.Property(e => e.ErrorMessage).HasColumnType("text");
    }
}
