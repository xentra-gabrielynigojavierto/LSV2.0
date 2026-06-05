using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

/// <summary>LSCC-01-004: EF Core table configuration for BlockedProviderAccessLogs.</summary>
public class BlockedProviderAccessLogConfiguration
    : IEntityTypeConfiguration<BlockedProviderAccessLog>
{
    public void Configure(EntityTypeBuilder<BlockedProviderAccessLog> builder)
    {
        builder.ToTable("cc_BlockedProviderAccessLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
               .HasColumnType("char(36)");

        builder.Property(x => x.TenantId)
               .HasColumnType("char(36)")
               .IsRequired(false);

        builder.Property(x => x.UserId)
               .HasColumnType("char(36)")
               .IsRequired(false);

        builder.Property(x => x.UserEmail)
               .HasMaxLength(256)
               .IsRequired(false);

        builder.Property(x => x.OrganizationId)
               .HasColumnType("char(36)")
               .IsRequired(false);

        builder.Property(x => x.ProviderId)
               .HasColumnType("char(36)")
               .IsRequired(false);

        builder.Property(x => x.ReferralId)
               .HasColumnType("char(36)")
               .IsRequired(false);

        builder.Property(x => x.FailureReason)
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(x => x.AttemptedAtUtc)
               .HasColumnType("datetime(6)")
               .IsRequired();

        // Operational query support: find latest attempts per user, sort by time descending.
        builder.HasIndex(x => new { x.UserId, x.AttemptedAtUtc })
               .HasDatabaseName("IX_BlockedProviderAccessLogs_UserId_AttemptedAtUtc");

        builder.HasIndex(x => x.AttemptedAtUtc)
               .HasDatabaseName("IX_BlockedProviderAccessLogs_AttemptedAtUtc");

        // BLK-PERF-01: Tenant-scoped dashboard counts filter on (TenantId, AttemptedAtUtc).
        // Without this, dashboard rolling-window queries scan all rows ordered by date.
        builder.HasIndex(x => new { x.TenantId, x.AttemptedAtUtc })
               .HasDatabaseName("IX_BlockedProviderAccessLogs_TenantId_AttemptedAtUtc");
    }
}
