namespace PlatformAuditEventService.DTOs.Query;

/// <summary>
/// Paginated result set for an audit event query.
/// Extends the standard page envelope with time-range metadata for UI convenience.
/// </summary>
public sealed class AuditEventQueryResponse
{
    /// <summary>The records matching the query on this page.</summary>
    public IReadOnlyList<AuditEventRecordResponse> Items { get; init; } = [];

    // ── Pagination ────────────────────────────────────────────────────────────

    /// <summary>Total number of records matching the full filter (across all pages).</summary>
    public long TotalCount { get; init; }

    /// <summary>Current 1-based page number.</summary>
    public int Page { get; init; }

    /// <summary>Number of records per page as applied by the service.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages for the current TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>True when a next page exists.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>True when a previous page exists.</summary>
    public bool HasPrev => Page > 1;

    // ── Time range metadata ───────────────────────────────────────────────────

    /// <summary>
    /// OccurredAtUtc of the earliest record in the full result set (not just this page).
    /// Null when TotalCount is zero. Useful for rendering time-range displays.
    /// </summary>
    public DateTimeOffset? EarliestOccurredAtUtc { get; init; }

    /// <summary>
    /// OccurredAtUtc of the latest record in the full result set.
    /// Null when TotalCount is zero.
    /// </summary>
    public DateTimeOffset? LatestOccurredAtUtc { get; init; }
}
