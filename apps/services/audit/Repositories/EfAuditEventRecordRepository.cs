using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using AuditRecordQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="AuditEventRecord"/>.
///
/// Append-only contract: no UPDATE or DELETE operations are exposed.
/// All queries use AsNoTracking() for read performance.
/// Writes use short-lived DbContext instances from the factory to keep
/// the transaction scope minimal.
///
/// Filter logic is centralised in <see cref="ApplyFilters"/> and shared between
/// the paginated <see cref="QueryAsync"/> and the streaming
/// <see cref="StreamForExportAsync"/> methods to guarantee consistent behaviour.
///
/// Visibility semantics in <see cref="ApplyFilters"/>:
///   The VisibilityScope enum is ordered from most-restricted (Platform=1) to
///   most-permissive (User=4) with Internal(5) never queryable.
///   MaxVisibility represents the *least-restricted* scope the caller may see.
///   Example: MaxVisibility=Tenant(2) → return records with scope ∈ {Tenant, Org, User}.
///   Internal records are always excluded regardless of the MaxVisibility value.
/// </summary>
public sealed class EfAuditEventRecordRepository : IAuditEventRecordRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfAuditEventRecordRepository> _logger;

    public EfAuditEventRecordRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfAuditEventRecordRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    // ── Write ──────────────────────────────────────────────────────────────────

    public async Task<AuditEventRecord> AppendAsync(
        AuditEventRecord record,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AuditEventRecords.Add(record);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "AuditEventRecord persisted: AuditId={AuditId} TenantId={TenantId}",
            record.AuditId, record.TenantId);

        return record;
    }

    // ── Point lookups ──────────────────────────────────────────────────────────

    public async Task<AuditEventRecord?> GetByAuditIdAsync(
        Guid                     auditId,
        AuditRecordQueryRequest? scopeFilter = null,
        CancellationToken        ct          = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Start from the full record set and apply the caller's authorized scope
        // constraints (TenantId, OrganizationId, ActorId, MaxVisibility, etc.) via
        // the same shared predicate pipeline used by QueryAsync and StreamForExportAsync.
        // The AuditId equality predicate is then added on top.
        IQueryable<AuditEventRecord> query = db.AuditEventRecords.AsNoTracking();

        if (scopeFilter is not null)
            query = ApplyFilters(query, scopeFilter);

        return await query.FirstOrDefaultAsync(r => r.AuditId == auditId, ct);
    }

    public async Task<bool> ExistsIdempotencyKeyAsync(
        string? key,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords
            .AsNoTracking()
            .AnyAsync(r => r.IdempotencyKey == key, ct);
    }

    public async Task<AuditEventRecord?> GetLatestInChainAsync(
        string? tenantId,
        string sourceSystem,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditEventRecords
            .AsNoTracking()
            .Where(r => r.SourceSystem == sourceSystem);

        if (tenantId is not null)
            query = query.Where(r => r.TenantId == tenantId);

        // Id is the auto-increment surrogate — ordering by it gives insertion order,
        // which is the correct semantic for "most recent record in the chain".
        return await query
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
    }

    // ── Aggregate ──────────────────────────────────────────────────────────────

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords.LongCountAsync(ct);
    }

    // ── Time-range aggregate ───────────────────────────────────────────────────

    public async Task<(DateTimeOffset? Earliest, DateTimeOffset? Latest)> GetOccurredAtRangeAsync(
        AuditRecordQueryRequest filter,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var filtered = ApplyFilters(db.AuditEventRecords.AsNoTracking(), filter);

        // SQLite cannot apply Min/Max aggregate operators on DateTimeOffset columns, and
        // also cannot ORDER BY DateTimeOffset. Use Id (auto-increment, time-ordered) instead.
        var earliest = await filtered
            .OrderBy(r => r.Id)
            .Select(r => (DateTimeOffset?)r.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);

        var latest = await filtered
            .OrderByDescending(r => r.Id)
            .Select(r => (DateTimeOffset?)r.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);

        return (earliest, latest);
    }

    // ── Paginated query ────────────────────────────────────────────────────────

    public async Task<PagedResult<AuditEventRecord>> QueryAsync(
        AuditRecordQueryRequest q,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var filtered = ApplyFilters(db.AuditEventRecords.AsNoTracking(), q);
        var sorted   = ApplySorting(filtered, q);

        var total    = await sorted.CountAsync(ct);
        var pageSize = Math.Max(1, Math.Min(q.PageSize, 500));
        var page     = Math.Max(1, q.Page);

        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEventRecord>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // ── Checkpoint hash streaming ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async IAsyncEnumerable<string?> StreamHashesForWindowAsync(
        DateTimeOffset fromRecordedAtUtc,
        DateTimeOffset toRecordedAtUtc,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Project only the Hash field — no need to load full entities for checkpoint generation.
        // Order by Id (surrogate auto-increment) for deterministic insertion-order traversal.
        var query = db.AuditEventRecords
            .AsNoTracking()
            .Where(r => r.RecordedAtUtc >= fromRecordedAtUtc && r.RecordedAtUtc < toRecordedAtUtc)
            .OrderBy(r => r.Id)
            .Select(r => r.Hash);

        await foreach (var hash in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return hash;
        }
    }

    // ── Streaming export ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// The DbContext lifetime spans the full enumeration — it is disposed when the
    /// caller's <c>await foreach</c> completes or when <paramref name="ct"/> fires.
    /// Do not capture the yielded records outside the loop scope; they hold no
    /// navigation-property proxies (AsNoTracking is always applied).
    /// </remarks>
    public async IAsyncEnumerable<AuditEventRecord> StreamForExportAsync(
        AuditRecordQueryRequest filter,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // DbContext is held open for the entire enumeration.
        // The factory pattern ensures it does not conflict with other operations.
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = ApplyFilters(db.AuditEventRecords.AsNoTracking(), filter)
            .OrderBy(r => r.Id);  // Insertion-order → deterministic export (SQLite-safe)

        await foreach (var record in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return record;
        }
    }

    // ── Retention enforcement ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEventRecord>> GetOldestEligibleAsync(
        DateTimeOffset    beforeRecordedAtUtc,
        int               batchSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEventRecords
            .AsNoTracking()
            .Where(r => r.RecordedAtUtc < beforeRecordedAtUtc)
            .OrderBy(r => r.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBatchAsync(
        IReadOnlyList<long> ids,
        CancellationToken   ct = default)
    {
        if (ids.Count == 0) return 0;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var deleted = await db.AuditEventRecords
            .Where(r => ids.Contains(r.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogWarning(
            "RETENTION DELETE: deleted {Count} audit event records from primary store.",
            deleted);

        return deleted;
    }

    // ── Shared filter + sort pipeline ─────────────────────────────────────────

    /// <summary>
    /// Applies all predicate filters from <paramref name="q"/> to the source queryable.
    /// Does not apply sorting or pagination — callers handle those separately.
    ///
    /// Visibility rule: Internal-scoped records are always excluded.
    /// When MaxVisibility is supplied, only records with VisibilityScope ≥ MaxVisibility
    /// are returned (i.e. at least as permissive as the caller's allowed maximum).
    /// </summary>
    private static IQueryable<AuditEventRecord> ApplyFilters(
        IQueryable<AuditEventRecord> source,
        AuditRecordQueryRequest q)
    {
        // ── Scope ──────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.TenantId))
            source = source.Where(r => r.TenantId == q.TenantId);

        if (!string.IsNullOrWhiteSpace(q.OrganizationId))
            source = source.Where(r => r.OrganizationId == q.OrganizationId);

        // ── Classification ─────────────────────────────────────────────────────
        if (q.Category.HasValue)
            source = source.Where(r => r.EventCategory == q.Category.Value);

        if (q.MinSeverity.HasValue)
            source = source.Where(r => r.Severity >= q.MinSeverity.Value);

        if (q.MaxSeverity.HasValue)
            source = source.Where(r => r.Severity <= q.MaxSeverity.Value);

        if (q.EventTypes is { Count: > 0 })
            source = source.Where(r => q.EventTypes.Contains(r.EventType));

        if (!string.IsNullOrWhiteSpace(q.SourceSystem))
            source = source.Where(r => r.SourceSystem == q.SourceSystem);

        if (!string.IsNullOrWhiteSpace(q.SourceService))
            source = source.Where(r => r.SourceService == q.SourceService);

        if (!string.IsNullOrWhiteSpace(q.SourceEnvironment))
            source = source.Where(r => r.SourceEnvironment == q.SourceEnvironment);

        // ── Actor / identity ───────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.ActorId))
            source = source.Where(r => r.ActorId == q.ActorId);

        if (q.ActorType.HasValue)
            source = source.Where(r => r.ActorType == q.ActorType.Value);

        // ── Entity / resource ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.EntityType))
            source = source.Where(r => r.EntityType == q.EntityType);

        if (!string.IsNullOrWhiteSpace(q.EntityId))
            source = source.Where(r => r.EntityId == q.EntityId);

        // ── Correlation ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.CorrelationId))
            source = source.Where(r => r.CorrelationId == q.CorrelationId);

        if (!string.IsNullOrWhiteSpace(q.RequestId))
            source = source.Where(r => r.RequestId == q.RequestId);

        if (!string.IsNullOrWhiteSpace(q.SessionId))
            source = source.Where(r => r.SessionId == q.SessionId);

        // ── Time range ─────────────────────────────────────────────────────────
        if (q.From.HasValue)
            source = source.Where(r => r.OccurredAtUtc >= q.From.Value);

        if (q.To.HasValue)
            source = source.Where(r => r.OccurredAtUtc < q.To.Value);

        // ── Visibility ─────────────────────────────────────────────────────────
        // Internal (5) is never queryable regardless of caller role.
        // Exact match (Visibility) takes precedence when both are supplied.
        // When MaxVisibility is supplied: return records with VisibilityScope ≥ MaxVisibility,
        // meaning at least as permissive as the caller's allowed level.
        // Example: MaxVisibility=Tenant(2) → VisibilityScope ∈ {Tenant(2), Org(3), User(4)}.
        //          Platform(1) records are excluded — they require super-admin access.
        if (q.Visibility.HasValue)
        {
            // Exact match — Internal never surfaced even if explicitly requested.
            if (q.Visibility.Value != VisibilityScope.Internal)
                source = source.Where(r => r.VisibilityScope == q.Visibility.Value);
            else
                source = source.Where(_ => false); // Internal always excluded
        }
        else if (q.MaxVisibility.HasValue)
        {
            source = source.Where(r =>
                r.VisibilityScope >= q.MaxVisibility.Value &&
                r.VisibilityScope != VisibilityScope.Internal);
        }
        else
        {
            source = source.Where(r => r.VisibilityScope != VisibilityScope.Internal);
        }

        // ── Text search ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.DescriptionContains))
            source = source.Where(r => r.Description.Contains(q.DescriptionContains));

        return source;
    }

    private static IQueryable<AuditEventRecord> ApplySorting(
        IQueryable<AuditEventRecord> source,
        AuditRecordQueryRequest q)
    {
        var desc = q.SortDescending;
        return q.SortBy?.ToLowerInvariant() switch
        {
            // DateTimeOffset columns cannot be used in ORDER BY on SQLite.
            // Id is an auto-increment long that preserves insertion order,
            // which is equivalent to sorting by RecordedAtUtc / OccurredAtUtc.
            "recordedat" or "recordedatutc" =>
                desc ? source.OrderByDescending(r => r.Id)
                     : source.OrderBy(r => r.Id),
            "severity" =>
                desc ? source.OrderByDescending(r => r.Severity)
                     : source.OrderBy(r => r.Severity),
            "sourcesystem" =>
                desc ? source.OrderByDescending(r => r.SourceSystem)
                     : source.OrderBy(r => r.SourceSystem),
            _ =>
                desc ? source.OrderByDescending(r => r.Id)
                     : source.OrderBy(r => r.Id),
        };
    }
}
