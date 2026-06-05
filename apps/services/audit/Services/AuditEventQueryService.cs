using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Query;
using PlatformAuditEventService.Mapping;
using PlatformAuditEventService.Repositories;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical implementation of <see cref="IAuditEventQueryService"/>.
///
/// Responsibilities:
/// - Delegate all persistence reads to <see cref="IAuditEventRecordRepository"/>.
/// - Apply response shaping rules from <see cref="QueryAuthOptions"/>:
///     - <c>ExposeIntegrityHash</c> controls whether Hash is included in responses.
///     - (Future) role-based IP/UserAgent redaction.
/// - Map <c>AuditEventRecord</c> entities → <c>AuditEventRecordResponse</c> DTOs
///   via <see cref="AuditEventRecordMapper"/>.
/// - Populate time-range metadata (<c>EarliestOccurredAtUtc</c>, <c>LatestOccurredAtUtc</c>)
///   using a single extra aggregate DB query per list request.
///
/// The service has no write dependencies — it is safe to register as Scoped
/// and used in both web request and background worker contexts.
/// </summary>
public sealed class AuditEventQueryService : IAuditEventQueryService
{
    private readonly IAuditEventRecordRepository _repository;
    private readonly QueryAuthOptions            _queryAuth;
    private readonly ILogger<AuditEventQueryService> _logger;

    public AuditEventQueryService(
        IAuditEventRecordRepository          repository,
        IOptions<QueryAuthOptions>           queryAuth,
        ILogger<AuditEventQueryService>      logger)
    {
        _repository = repository;
        _queryAuth  = queryAuth.Value;
        _logger     = logger;
    }

    // ── Single record ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AuditEventRecordResponse?> GetByAuditIdAsync(
        Guid                    auditId,
        AuditEventQueryRequest? scopeFilter = null,
        CancellationToken       ct          = default)
    {
        var record = await _repository.GetByAuditIdAsync(auditId, scopeFilter, ct);
        if (record is null)
        {
            _logger.LogDebug(
                "GetByAuditIdAsync: AuditId={AuditId} ScopeFilter={HasFilter} not found.",
                auditId, scopeFilter is not null ? "yes" : "none");
            return null;
        }

        return AuditEventRecordMapper.ToResponse(
            record,
            exposeHash: _queryAuth.ExposeIntegrityHash);
    }

    // ── Paginated query ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AuditEventQueryResponse> QueryAsync(
        AuditEventQueryRequest request,
        CancellationToken ct = default)
    {
        // Enforce the configured page-size cap regardless of what the caller requested.
        request.PageSize = Math.Clamp(request.PageSize, 1, _queryAuth.MaxPageSize);

        // Execute the paginated query and the time-range aggregate in parallel.
        // Both use the same filter predicates; the range query issues a single
        // GROUP BY 1 aggregate and is cheap for indexed columns.
        var (pagedTask, rangeTask) = (
            _repository.QueryAsync(request, ct),
            _repository.GetOccurredAtRangeAsync(request, ct)
        );

        var paged = await pagedTask;
        var (earliest, latest) = await rangeTask;

        _logger.LogDebug(
            "QueryAsync: TotalCount={Total} Page={Page} PageSize={PageSize}",
            paged.TotalCount, paged.Page, paged.PageSize);

        var items = AuditEventRecordMapper.ToResponseList(
            paged.Items,
            exposeHash: _queryAuth.ExposeIntegrityHash);

        return new AuditEventQueryResponse
        {
            Items              = items,
            TotalCount         = paged.TotalCount,
            Page               = paged.Page,
            PageSize           = paged.PageSize,
            EarliestOccurredAtUtc = earliest,
            LatestOccurredAtUtc   = latest,
        };
    }
}
