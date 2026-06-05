using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="LegalHold"/>.
///
/// Table: LegalHolds
///   - bigint AUTO_INCREMENT PK (internal surrogate)
///   - char(36) HoldId — unique public identifier
///   - char(36) AuditId — references AuditEventRecords.AuditId (soft reference; no FK constraint
///     to avoid cascade coupling with the append-only records table)
///   - varchar LegalAuthority — indexed for compliance workflows that query by authority
///   - HeldAtUtc / ReleasedAtUtc — datetime(6) UTC; ReleasedAtUtc nullable (active = null)
///
/// Indexes:
///   IX_LegalHolds_AuditId            — point lookup by record
///   IX_LegalHolds_LegalAuthority     — authority-grouped compliance queries
///   IX_LegalHolds_ReleasedAtUtc      — active hold scan (WHERE ReleasedAtUtc IS NULL)
/// </summary>
public sealed class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
{
    public void Configure(EntityTypeBuilder<LegalHold> builder)
    {
        builder.ToTable("aud_LegalHolds");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
               .ValueGeneratedOnAdd();

        builder.Property(h => h.HoldId)
               .IsRequired()
               .HasColumnType("char(36)");

        builder.HasIndex(h => h.HoldId)
               .IsUnique()
               .HasDatabaseName("IX_LegalHolds_HoldId_Unique");

        builder.Property(h => h.AuditId)
               .IsRequired()
               .HasColumnType("char(36)");

        builder.HasIndex(h => h.AuditId)
               .HasDatabaseName("IX_LegalHolds_AuditId");

        builder.Property(h => h.HeldByUserId)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(h => h.HeldAtUtc)
               .IsRequired()
               .HasColumnType("datetime(6)");

        builder.Property(h => h.ReleasedAtUtc)
               .HasColumnType("datetime(6)");

        builder.HasIndex(h => h.ReleasedAtUtc)
               .HasDatabaseName("IX_LegalHolds_ReleasedAtUtc");

        builder.Property(h => h.ReleasedByUserId)
               .HasMaxLength(256);

        builder.Property(h => h.LegalAuthority)
               .IsRequired()
               .HasMaxLength(512);

        builder.HasIndex(h => h.LegalAuthority)
               .HasDatabaseName("IX_LegalHolds_LegalAuthority");

        builder.Property(h => h.Notes)
               .HasMaxLength(2000);
    }
}
