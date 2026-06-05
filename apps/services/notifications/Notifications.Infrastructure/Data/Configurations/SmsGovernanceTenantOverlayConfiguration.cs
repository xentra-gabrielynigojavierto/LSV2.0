using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class SmsGovernanceTenantOverlayConfiguration
    : IEntityTypeConfiguration<SmsGovernanceTenantOverlay>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceTenantOverlay> builder)
    {
        builder.ToTable("ntf_SmsGovernanceTenantOverlays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.TenantId).IsRequired().HasColumnType("char(36)");
        builder.Property(x => x.RulePackId).HasColumnType("char(36)");
        builder.Property(x => x.RuleId).HasColumnType("char(36)");

        builder.Property(x => x.OverlayType)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.OverlayState)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");

        builder.Property(x => x.OverrideJson).HasMaxLength(4000).HasColumnType("varchar(4000)");
        builder.Property(x => x.Priority).HasColumnType("int");
        builder.Property(x => x.Enabled).HasColumnType("tinyint(1)");

        builder.Property(x => x.EffectiveFrom).HasColumnType("datetime(6)");
        builder.Property(x => x.EffectiveTo).HasColumnType("datetime(6)");

        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.CreatedBy).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.UpdatedBy).HasMaxLength(200).HasColumnType("varchar(200)");

        builder.HasIndex(x => new { x.TenantId, x.Enabled, x.Priority })
               .HasDatabaseName("IX_ntf_SmsGovTenantOverlay_Tenant_Enabled_Priority");
        builder.HasIndex(x => new { x.TenantId, x.RulePackId })
               .HasDatabaseName("IX_ntf_SmsGovTenantOverlay_Tenant_Pack");
        builder.HasIndex(x => new { x.TenantId, x.RuleId })
               .HasDatabaseName("IX_ntf_SmsGovTenantOverlay_Tenant_Rule");
        builder.HasIndex(x => new { x.OverlayType, x.Enabled })
               .HasDatabaseName("IX_ntf_SmsGovTenantOverlay_Type_Enabled");
    }
}
