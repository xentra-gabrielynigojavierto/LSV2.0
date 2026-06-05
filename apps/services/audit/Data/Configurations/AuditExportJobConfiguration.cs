using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core Fluent API mapping for <see cref="AuditExportJob"/>.
///
/// Schema design notes:
/// - <c>ExportId</c> (char 36) is the stable public identifier; <c>Id</c> is the
///   internal surrogate PK for efficient joins and pagination.
/// - <c>Status</c> is stored as <c>tinyint</c> for compact storage; the enum's
///   integer backing values are stable (explicit in the enum definition).
/// - <c>FilterJson</c> uses <c>text</c>; export filter predicates are bounded
///   in size (well under 64 KB in practice).
/// - <c>FilePath</c> is <c>varchar(1000)</c> to accommodate deep S3/Azure paths
///   and pre-signed URL lengths without an unbounded column.
/// - <c>ErrorMessage</c> is <c>text</c> to handle verbose stack traces or
///   multi-line error descriptions from the export worker.
/// </summary>
public sealed class AuditExportJobConfiguration : IEntityTypeConfiguration<AuditExportJob>
{
    public void Configure(EntityTypeBuilder<AuditExportJob> entity)
    {
        // ── Table ────────────────────────────────────────────────────────────
        entity.ToTable("aud_AuditExportJobs");

        // ── Primary key ──────────────────────────────────────────────────────
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // ── Public identifier ─────────────────────────────────────────────────
        entity.Property(e => e.ExportId)
            .IsRequired()
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        // ── Identity ──────────────────────────────────────────────────────────
        entity.Property(e => e.RequestedBy)
            .IsRequired()
            .HasMaxLength(200);

        // ── Scope ─────────────────────────────────────────────────────────────
        entity.Property(e => e.ScopeType)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        entity.Property(e => e.ScopeId)
            .HasMaxLength(200);

        // ── Filter ────────────────────────────────────────────────────────────
        entity.Property(e => e.FilterJson)
            .HasColumnType("text");

        // ── Output configuration ──────────────────────────────────────────────
        // "Json" | "Csv" | "Ndjson" — max 10 chars with room for future formats
        entity.Property(e => e.Format)
            .IsRequired()
            .HasMaxLength(20);

        // ── Lifecycle ─────────────────────────────────────────────────────────
        entity.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        // Long enough for S3/Azure paths and pre-signed URLs (presigned can be long,
        // but the stored value is the base path; the URL is generated at read time)
        entity.Property(e => e.FilePath)
            .HasMaxLength(1000);

        entity.Property(e => e.ErrorMessage)
            .HasColumnType("text");

        // ── Timestamps ────────────────────────────────────────────────────────
        entity.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        entity.Property(e => e.CompletedAtUtc)
            .HasColumnType("datetime(6)");

        // Set when Status=Completed; null otherwise.
        entity.Property(e => e.RecordCount)
            .HasColumnType("bigint");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Public identifier — unique
        entity.HasIndex(e => e.ExportId)
            .IsUnique()
            .HasDatabaseName("UX_AuditExportJobs_ExportId");

        // Status polling — find pending/processing jobs for the worker
        entity.HasIndex(e => e.Status)
            .HasDatabaseName("IX_AuditExportJobs_Status");

        // Requester history — "show me my exports"
        entity.HasIndex(e => e.RequestedBy)
            .HasDatabaseName("IX_AuditExportJobs_RequestedBy");

        // Scope lookup — find all exports for a tenant/org
        entity.HasIndex(e => new { e.ScopeType, e.ScopeId })
            .HasDatabaseName("IX_AuditExportJobs_ScopeType_ScopeId");

        // Time-ordered listing — newest-first default sort
        entity.HasIndex(e => e.CreatedAtUtc)
            .HasDatabaseName("IX_AuditExportJobs_CreatedAtUtc");

        // Composite: requester + status (paginate my pending/completed exports)
        entity.HasIndex(e => new { e.RequestedBy, e.Status, e.CreatedAtUtc })
            .HasDatabaseName("IX_AuditExportJobs_RequestedBy_Status_CreatedAt");
    }
}
