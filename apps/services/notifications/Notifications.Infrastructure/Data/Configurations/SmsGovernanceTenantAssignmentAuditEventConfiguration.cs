using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

internal sealed class SmsGovernanceTenantAssignmentAuditEventConfiguration
    : IEntityTypeConfiguration<SmsGovernanceTenantAssignmentAuditEvent>
{
    public void Configure(EntityTypeBuilder<SmsGovernanceTenantAssignmentAuditEvent> builder)
    {
        builder.ToTable("ntf_SmsGovernanceTenantAssignmentAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("char(36)");

        builder.Property(x => x.TenantId).IsRequired().HasColumnType("char(36)");
        builder.Property(x => x.AssignmentId).HasColumnType("char(36)");
        builder.Property(x => x.OverlayId).HasColumnType("char(36)");

        builder.Property(x => x.EventType)
               .IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(x => x.PreviousState).HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.NewState).HasMaxLength(50).HasColumnType("varchar(50)");
        builder.Property(x => x.Actor).HasMaxLength(200).HasColumnType("varchar(200)");
        builder.Property(x => x.Reason).HasMaxLength(1000).HasColumnType("varchar(1000)");
        builder.Property(x => x.MetadataJson).HasMaxLength(4000).HasColumnType("varchar(4000)");

        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime(6)");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
               .HasDatabaseName("IX_ntf_SmsGovTenantAudit_Tenant_Dt");
        builder.HasIndex(x => new { x.AssignmentId, x.CreatedAt })
               .HasDatabaseName("IX_ntf_SmsGovTenantAudit_Assignment_Dt");
        builder.HasIndex(x => new { x.OverlayId, x.CreatedAt })
               .HasDatabaseName("IX_ntf_SmsGovTenantAudit_Overlay_Dt");
        builder.HasIndex(x => new { x.EventType, x.CreatedAt })
               .HasDatabaseName("IX_ntf_SmsGovTenantAudit_EventType_Dt");
    }
}
