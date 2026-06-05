using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class GovernanceFederationAuditEventConfiguration
    : IEntityTypeConfiguration<GovernanceFederationAuditEvent>
{
    public void Configure(EntityTypeBuilder<GovernanceFederationAuditEvent> builder)
    {
        builder.ToTable("ntf_GovernanceFederationAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.TenantId).HasColumnType("char(36)");
        builder.Property(x => x.ChannelType).HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.FederationGroup).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.EntityType)
               .IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(x => x.EntityId).HasColumnType("char(36)");
        builder.Property(x => x.EventType)
               .IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(x => x.PreviousState).HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(x => x.NewState).HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(x => x.Actor).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.Reason).HasMaxLength(1000).HasColumnType("varchar(1000)");
        builder.Property(x => x.MetadataJson).HasMaxLength(4000).HasColumnType("varchar(4000)");
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
               .HasDatabaseName("IX_ntf_GovFedAudit_Tenant_Date");
        builder.HasIndex(x => new { x.ChannelType, x.CreatedAt })
               .HasDatabaseName("IX_ntf_GovFedAudit_Channel_Date");
        builder.HasIndex(x => new { x.EventType, x.CreatedAt })
               .HasDatabaseName("IX_ntf_GovFedAudit_EventType_Date");
        builder.HasIndex(x => new { x.EntityType, x.EntityId })
               .HasDatabaseName("IX_ntf_GovFedAudit_Entity");
    }
}
