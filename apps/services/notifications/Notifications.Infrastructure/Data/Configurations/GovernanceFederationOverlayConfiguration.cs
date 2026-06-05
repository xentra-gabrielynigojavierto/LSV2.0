using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class GovernanceFederationOverlayConfiguration
    : IEntityTypeConfiguration<GovernanceFederationOverlay>
{
    public void Configure(EntityTypeBuilder<GovernanceFederationOverlay> builder)
    {
        builder.ToTable("ntf_GovernanceFederationOverlays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.TenantId).HasColumnType("char(36)");
        builder.Property(x => x.ChannelType)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.RulePackId).HasColumnType("char(36)");
        builder.Property(x => x.RuleId).HasColumnType("char(36)");

        builder.Property(x => x.OverlayType)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.OverlayState)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.OverlayJson).HasMaxLength(4000).HasColumnType("varchar(4000)");

        builder.Property(x => x.Priority).HasColumnType("int");
        builder.Property(x => x.Enabled).IsRequired().HasColumnType("tinyint(1)");
        builder.Property(x => x.EffectiveFrom).HasColumnType("datetime(6)");
        builder.Property(x => x.EffectiveTo).HasColumnType("datetime(6)");

        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.CreatedBy).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.UpdatedBy).HasMaxLength(200).HasColumnType("varchar(200)");

        builder.HasIndex(x => new { x.ChannelType, x.Enabled, x.Priority })
               .HasDatabaseName("IX_ntf_GovFedOverlay_Channel_Enabled_Priority");
        builder.HasIndex(x => new { x.TenantId, x.ChannelType, x.Enabled })
               .HasDatabaseName("IX_ntf_GovFedOverlay_Tenant_Channel_Enabled");
        builder.HasIndex(x => new { x.RulePackId, x.ChannelType })
               .HasDatabaseName("IX_ntf_GovFedOverlay_Pack_Channel");
        builder.HasIndex(x => new { x.RuleId, x.ChannelType })
               .HasDatabaseName("IX_ntf_GovFedOverlay_Rule_Channel");
        builder.HasIndex(x => new { x.OverlayType, x.Enabled })
               .HasDatabaseName("IX_ntf_GovFedOverlay_Type_Enabled");
    }
}
