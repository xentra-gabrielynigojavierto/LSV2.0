using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core Fluent API mapping for <see cref="IntegrityCheckpoint"/>.
///
/// Schema design notes:
/// - <c>AggregateHash</c> is stored as <c>varchar(64)</c> — HMAC-SHA256 hex output
///   is always exactly 64 hexadecimal characters.
/// - <c>RecordCount</c> uses <c>bigint</c> to accommodate audit stores that grow
///   beyond int range over multi-year retention windows.
/// - <c>CheckpointType</c> is an open string; no enum column type is used so that
///   new cadences can be introduced without a schema migration.
/// - No UPDATE or DELETE is expected; the EF configuration does not explicitly
///   restrict this, but the repository layer must enforce append-only access.
/// </summary>
public sealed class IntegrityCheckpointConfiguration : IEntityTypeConfiguration<IntegrityCheckpoint>
{
    public void Configure(EntityTypeBuilder<IntegrityCheckpoint> entity)
    {
        // ── Table ────────────────────────────────────────────────────────────
        entity.ToTable("aud_IntegrityCheckpoints");

        // ── Primary key ──────────────────────────────────────────────────────
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // ── Classification ────────────────────────────────────────────────────
        // Open string: "hourly" | "daily" | "manual" | custom cadence label
        entity.Property(e => e.CheckpointType)
            .IsRequired()
            .HasMaxLength(100);

        // ── Time window ───────────────────────────────────────────────────────
        entity.Property(e => e.FromRecordedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        entity.Property(e => e.ToRecordedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        // ── Integrity ─────────────────────────────────────────────────────────
        // HMAC-SHA256 hex = exactly 64 chars
        entity.Property(e => e.AggregateHash)
            .IsRequired()
            .HasMaxLength(64);

        entity.Property(e => e.RecordCount)
            .IsRequired()
            .HasColumnType("bigint");

        // ── Timestamps ────────────────────────────────────────────────────────
        entity.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Time window lookup — the primary verification query pattern:
        // "find the checkpoint that covers this time range"
        entity.HasIndex(e => new { e.FromRecordedAtUtc, e.ToRecordedAtUtc })
            .HasDatabaseName("IX_IntegrityCheckpoints_Window");

        // Cadence type — list all checkpoints of a given type (e.g. all daily)
        entity.HasIndex(e => e.CheckpointType)
            .HasDatabaseName("IX_IntegrityCheckpoints_CheckpointType");

        // Chronological listing — newest-first display and cleanup queries
        entity.HasIndex(e => e.CreatedAtUtc)
            .HasDatabaseName("IX_IntegrityCheckpoints_CreatedAtUtc");

        // Composite: type + window (find the checkpoint for a specific cadence + period)
        entity.HasIndex(e => new { e.CheckpointType, e.FromRecordedAtUtc })
            .HasDatabaseName("IX_IntegrityCheckpoints_CheckpointType_FromAt");
    }
}
