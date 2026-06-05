using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core Fluent API mapping for <see cref="AuditEventRecord"/>.
///
/// Schema design notes:
/// - <c>Id</c> (bigint AUTO_INCREMENT) is the clustered PK for scan/sort efficiency.
///   <c>AuditId</c> (char 36) is the stable public identifier exposed via the API.
/// - Enum columns use <c>tinyint</c> with int conversion; values fit within signed 8-bit
///   range (all enum members have explicit int backing ≤ 9).
/// - <c>BeforeJson</c> and <c>AfterJson</c> use <c>mediumtext</c> (up to 16 MB) because
///   full domain-object snapshots may be large. Other JSON columns use <c>text</c> (64 KB).
/// - <c>DateTimeOffset</c> fields are stored as <c>datetime(6)</c> UTC; Pomelo strips the
///   offset on write and restores as UTC on read. All timestamps are UTC by convention.
/// - No <c>HasDefaultValue</c> or <c>HasDefaultValueSql</c> on required fields — values
///   must be supplied by the ingest pipeline, never silently defaulted by the database.
/// - The <c>IdempotencyKey</c> unique index allows multiple NULLs in MySQL 8 (each NULL is
///   treated as distinct in a UNIQUE index), satisfying the optional-key dedup contract.
/// </summary>
public sealed class AuditEventRecordConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
    public void Configure(EntityTypeBuilder<AuditEventRecord> entity)
    {
        // ── Table ────────────────────────────────────────────────────────────
        entity.ToTable("aud_AuditEventRecords");

        // ── Primary key ──────────────────────────────────────────────────────
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        // ── Public identifier ─────────────────────────────────────────────────
        entity.Property(e => e.AuditId)
            .IsRequired()
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        entity.Property(e => e.EventId)
            .HasColumnType("char(36)");

        // ── Classification ────────────────────────────────────────────────────
        entity.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.EventCategory)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        // ── Source provenance ─────────────────────────────────────────────────
        entity.Property(e => e.SourceSystem)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.SourceService)
            .HasMaxLength(200);

        entity.Property(e => e.SourceEnvironment)
            .HasMaxLength(100);

        // ── Scope / tenancy ───────────────────────────────────────────────────
        entity.Property(e => e.PlatformId)
            .HasColumnType("char(36)");

        entity.Property(e => e.TenantId)
            .HasMaxLength(100);

        entity.Property(e => e.OrganizationId)
            .HasMaxLength(100);

        entity.Property(e => e.UserScopeId)
            .HasMaxLength(200);

        entity.Property(e => e.ScopeType)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        // ── Actor / identity ──────────────────────────────────────────────────
        entity.Property(e => e.ActorId)
            .HasMaxLength(200);

        entity.Property(e => e.ActorType)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        entity.Property(e => e.ActorName)
            .HasMaxLength(300);

        // IPv4 = 15 chars, IPv6 max = 45 chars
        entity.Property(e => e.ActorIpAddress)
            .HasMaxLength(45);

        entity.Property(e => e.ActorUserAgent)
            .HasMaxLength(500);

        // ── Target entity ─────────────────────────────────────────────────────
        entity.Property(e => e.EntityType)
            .HasMaxLength(200);

        entity.Property(e => e.EntityId)
            .HasMaxLength(200);

        // ── Action payload ────────────────────────────────────────────────────
        entity.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(2000);

        // Full entity snapshots — up to 16 MB to accommodate large domain objects
        entity.Property(e => e.BeforeJson)
            .HasColumnType("mediumtext");

        entity.Property(e => e.AfterJson)
            .HasColumnType("mediumtext");

        // Typically small supplementary context — 64 KB text is sufficient
        entity.Property(e => e.MetadataJson)
            .HasColumnType("text");

        // ── Correlation / tracing ─────────────────────────────────────────────
        entity.Property(e => e.CorrelationId)
            .HasMaxLength(200);

        entity.Property(e => e.RequestId)
            .HasMaxLength(200);

        entity.Property(e => e.SessionId)
            .HasMaxLength(200);

        // ── Access control ────────────────────────────────────────────────────
        entity.Property(e => e.VisibilityScope)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        entity.Property(e => e.Severity)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnType("tinyint");

        // ── Timestamps ────────────────────────────────────────────────────────
        entity.Property(e => e.OccurredAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        entity.Property(e => e.RecordedAtUtc)
            .IsRequired()
            .HasColumnType("datetime(6)");

        // ── Integrity chain ───────────────────────────────────────────────────
        // HMAC-SHA256 hex = 64 chars
        entity.Property(e => e.Hash)
            .HasMaxLength(64);

        entity.Property(e => e.PreviousHash)
            .HasMaxLength(64);

        // ── Deduplication ─────────────────────────────────────────────────────
        // 300 chars: accommodates source system + resource type + event ID composites
        entity.Property(e => e.IdempotencyKey)
            .HasMaxLength(300);

        // ── Replay flag ───────────────────────────────────────────────────────
        // bool maps to tinyint(1) in Pomelo by default
        entity.Property(e => e.IsReplay)
            .IsRequired()
            .HasDefaultValue(false);

        // ── Tags ─────────────────────────────────────────────────────────────
        entity.Property(e => e.TagsJson)
            .HasColumnType("text");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Public identifier — unique, used by GET /api/auditevents/{auditId}
        entity.HasIndex(e => e.AuditId)
            .IsUnique()
            .HasDatabaseName("UX_AuditEventRecords_AuditId");

        // ─ Required indexes per spec ─────────────────────────────────────────

        // TenantId — also covered by the composite below, but kept for explicit
        // tenant-scoped COUNT queries that don't include a time range.
        entity.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_AuditEventRecords_TenantId");

        // OrganizationId — org-scoped audit trail queries
        entity.HasIndex(e => e.OrganizationId)
            .HasDatabaseName("IX_AuditEventRecords_OrganizationId");

        // ActorId — "show me everything this user/service did"
        entity.HasIndex(e => e.ActorId)
            .HasDatabaseName("IX_AuditEventRecords_ActorId");

        // EntityId — "show me all events for this resource"
        // Composite with EntityType: OR-ing two nullable columns is expensive without it
        entity.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("IX_AuditEventRecords_EntityType_EntityId");

        // EventType — per-event-type feeds and alerting
        entity.HasIndex(e => e.EventType)
            .HasDatabaseName("IX_AuditEventRecords_EventType");

        // EventCategory — retention policy selection, category dashboards
        entity.HasIndex(e => e.EventCategory)
            .HasDatabaseName("IX_AuditEventRecords_EventCategory");

        // CorrelationId — distributed trace reconstruction across services
        entity.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_AuditEventRecords_CorrelationId");

        // RequestId — request-scoped event lookup
        entity.HasIndex(e => e.RequestId)
            .HasDatabaseName("IX_AuditEventRecords_RequestId");

        // SessionId — session-scoped event lookup
        entity.HasIndex(e => e.SessionId)
            .HasDatabaseName("IX_AuditEventRecords_SessionId");

        // OccurredAtUtc — global time-range queries without a tenant constraint
        entity.HasIndex(e => e.OccurredAtUtc)
            .HasDatabaseName("IX_AuditEventRecords_OccurredAtUtc");

        // RecordedAtUtc — integrity checkpoint computation window
        entity.HasIndex(e => e.RecordedAtUtc)
            .HasDatabaseName("IX_AuditEventRecords_RecordedAtUtc");

        // VisibilityScope — fast pre-filter before role claim evaluation
        entity.HasIndex(e => e.VisibilityScope)
            .HasDatabaseName("IX_AuditEventRecords_VisibilityScope");

        // IdempotencyKey — UNIQUE; MySQL 8 allows multiple NULLs in a UNIQUE index
        // (each NULL is treated as a distinct non-equal value), satisfying the
        // optional-key dedup contract without a partial/filtered index workaround.
        entity.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_AuditEventRecords_IdempotencyKey");

        // ─ Composite covering indexes (high-traffic query patterns) ────────────

        // Primary tenant time-range query: TenantId + OccurredAtUtc
        // Covers the most frequent compliance/dashboard pattern.
        entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEventRecords_TenantId_OccurredAt");

        // Tenant + category composite for category-filtered tenant dashboards
        entity.HasIndex(e => new { e.TenantId, e.EventCategory, e.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEventRecords_TenantId_Category_OccurredAt");

        // Severity + RecordedAtUtc for security alert feeds (high/critical recent events)
        entity.HasIndex(e => new { e.Severity, e.RecordedAtUtc })
            .HasDatabaseName("IX_AuditEventRecords_Severity_RecordedAt");
    }
}
