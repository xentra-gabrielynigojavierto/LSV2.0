using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core Fluent API mapping for <see cref="IngestSourceRegistration"/>.
///
/// Schema design notes:
/// - The (SourceSystem, SourceService) pair carries a UNIQUE constraint to prevent
///   duplicate registrations. NULL SourceService is permitted (means "all services
///   in the system"), and MySQL 8 UNIQUE indexes treat NULLs as distinct, so
///   multiple registrations with a NULL SourceService would each be unique only
///   per SourceSystem. The application layer must enforce at-most-one NULL-service
///   registration per SourceSystem if stricter dedup is needed.
/// - <c>IsActive</c> defaults to true at the database level to match the entity
///   initializer; this prevents accidental deactivation of records written without
///   an explicit value.
/// - <c>Notes</c> uses <c>text</c> to support verbose operator documentation.
/// </summary>
public sealed class IngestSourceRegistrationConfiguration : IEntityTypeConfiguration<IngestSourceRegistration>
{
    public void Configure(EntityTypeBuilder<IngestSourceRegistration> entity)
    {
        // ── Table ────────────────────────────────────────────────────────────
        entity.ToTable("aud_IngestSourceRegistrations");

        // ── Primary key ──────────────────────────────────────────────────────
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // ── Identity ──────────────────────────────────────────────────────────
        entity.Property(e => e.SourceSystem)
            .IsRequired()
            .HasMaxLength(200);

        // NULL means "all services within the system"
        entity.Property(e => e.SourceService)
            .HasMaxLength(200);

        // ── State ─────────────────────────────────────────────────────────────
        // tinyint(1) via Pomelo default bool mapping
        entity.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Documentation ─────────────────────────────────────────────────────
        entity.Property(e => e.Notes)
            .HasColumnType("text");

        // ── Timestamps ────────────────────────────────────────────────────────
        entity.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Primary lookup key: find the registration for a given source
        // UNIQUE: prevents duplicate registrations for the same (system, service) pair.
        // MySQL 8 allows multiple NULLs in a UNIQUE index, so NULL SourceService
        // means this covers "all services" without conflicting with specific services.
        entity.HasIndex(e => new { e.SourceSystem, e.SourceService })
            .IsUnique()
            .HasDatabaseName("UX_IngestSourceRegistrations_SourceSystem_SourceService");

        // IsActive filter — quickly find all active or paused sources
        entity.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_IngestSourceRegistrations_IsActive");
    }
}
