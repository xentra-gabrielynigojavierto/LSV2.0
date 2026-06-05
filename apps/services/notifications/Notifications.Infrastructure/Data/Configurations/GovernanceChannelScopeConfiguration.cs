using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class GovernanceChannelScopeConfiguration
    : IEntityTypeConfiguration<GovernanceChannelScope>
{
    public void Configure(EntityTypeBuilder<GovernanceChannelScope> builder)
    {
        builder.ToTable("ntf_GovernanceChannelScopes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.ChannelType)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.ScopeMode)
               .IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.Enabled).IsRequired().HasColumnType("tinyint(1)");
        builder.Property(x => x.Priority).HasColumnType("int");
        builder.Property(x => x.Description).HasMaxLength(500).HasColumnType("varchar(500)");

        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime(6)");
        builder.Property(x => x.CreatedBy).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.UpdatedBy).HasMaxLength(200).HasColumnType("varchar(200)");

        builder.HasIndex(x => new { x.ChannelType, x.Enabled })
               .HasDatabaseName("IX_ntf_GovChannelScope_Channel_Enabled");
        builder.HasIndex(x => new { x.ScopeMode, x.Enabled })
               .HasDatabaseName("IX_ntf_GovChannelScope_Mode_Enabled");
        builder.HasIndex(x => x.Priority)
               .HasDatabaseName("IX_ntf_GovChannelScope_Priority");
    }
}
