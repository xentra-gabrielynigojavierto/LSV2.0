using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.DTOs.Integrity;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

using IngestRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Integrity checkpoint retrieval and on-demand generation endpoints.
///
/// Route prefix: /audit/integrity
///
/// Authorization:
///   GET /audit/integrity/checkpoints              — requires TenantAdmin scope or higher.
///   POST /audit/integrity/checkpoints/generate    — requires PlatformAdmin scope only.
///
/// Both endpoints resolve the caller context from HttpContext.Items,
/// populated by <see cref="Middleware.QueryAuthMiddleware"/> for the /audit/* prefix.
///
/// Checkpoints are append-only. A generation request always creates a new record;
/// existing checkpoint records are never updated or deleted.
///
/// LS-ID-TNT-017-003:
///   Successful checkpoint generation now emits <c>audit.integrity.checkpoint.generated</c>
///   via the centralized ingestion pipeline. Fire-and-observe; the 201 response is never
///   gated on audit publish success.
/// </summary>
[ApiController]
[Route("audit/integrity")]
[Produces("application/json")]
public sealed class IntegrityCheckpointController : ControllerBase
{
    private readonly IIntegrityCheckpointService              _service;
    private readonly IAuditEventIngestionService              _ingestionService;
    private readonly ILogger<IntegrityCheckpointController>   _logger;

    public IntegrityCheckpointController(
        IIntegrityCheckpointService             service,
        IAuditEventIngestionService             ingestionService,
        ILogger<IntegrityCheckpointController>  logger)
    {
        _service          = service;
        _ingestionService = ingestionService;
        _logger           = logger;
    }

    // ── GET /audit/integrity/checkpoints ──────────────────────────────────────

    /// <summary>
    /// Retrieve a paginated list of persisted integrity checkpoints.
    ///
    /// All filter parameters are optional. Results are returned newest-first.
    ///
    /// Caller scope requirement: <c>TenantAdmin</c> or higher.
    /// Non-PlatformAdmin callers see the same checkpoint history — checkpoints are
    /// not tenant-scoped in v1 (they cover the full record store). This may be
    /// restricted to PlatformAdmin in a future step.
    /// </summary>
    /// <param name="query">Optional filter and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List returned successfully.</response>
    /// <response code="401">No valid credentials presented.</response>
    /// <response code="403">Caller scope is below TenantAdmin.</response>
    [HttpGet("checkpoints")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IntegrityCheckpointResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                                   StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                                   StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListCheckpoints(
        [FromQuery] CheckpointListQuery query,
        CancellationToken ct)
    {
        var deny = RequireScope(CallerScope.TenantAdmin);
        if (deny is not null) return deny;

        var traceId = TraceIdAccessor.Current();
        var result  = await _service.ListAsync(query, ct);

        _logger.LogDebug(
            "GET /audit/integrity/checkpoints → TotalCount={Total} TraceId={TraceId}",
            result.TotalCount, traceId);

        return Ok(ApiResponse<PagedResult<IntegrityCheckpointResponse>>.Ok(result, traceId: traceId));
    }

    // ── POST /audit/integrity/checkpoints/generate ────────────────────────────

    /// <summary>
    /// Generate a new integrity checkpoint on demand over a specified time window.
    ///
    /// The service streams all audit event record hashes whose <c>RecordedAtUtc</c>
    /// falls within <c>[FromRecordedAtUtc, ToRecordedAtUtc)</c>, concatenates them in
    /// ascending insertion-order (by surrogate Id), and computes an aggregate hash.
    /// The result is persisted as a new checkpoint record.
    ///
    /// Caller scope requirement: <c>PlatformAdmin</c> only.
    /// This operation is intentionally restricted because it exercises the full hash
    /// pipeline and creates compliance records — it should only be triggered by
    /// authorised platform operators or the background job.
    /// </summary>
    /// <param name="request">Checkpoint type label and time window bounds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Checkpoint generated and persisted.</response>
    /// <response code="400">Invalid request (e.g. inverted time window).</response>
    /// <response code="401">No valid credentials presented.</response>
    /// <response code="403">Caller scope is below PlatformAdmin.</response>
    [HttpPost("checkpoints/generate")]
    [ProducesResponseType(typeof(ApiResponse<IntegrityCheckpointResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>),                      StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                      StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                      StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateCheckpoint(
        [FromBody] GenerateCheckpointRequest request,
        CancellationToken ct)
    {
        var deny = RequireScope(CallerScope.PlatformAdmin);
        if (deny is not null) return deny;

        var traceId = TraceIdAccessor.Current();

        if (request.ToRecordedAtUtc <= request.FromRecordedAtUtc)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "ToRecordedAtUtc must be strictly after FromRecordedAtUtc.",
                traceId: traceId));
        }

        var caller = HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
                  && raw is IQueryCallerContext ctx
                     ? ctx
                     : QueryCallerContext.Anonymous();

        var result = await _service.GenerateAsync(request, ct);

        _logger.LogInformation(
            "Checkpoint generated on demand. Id={Id} Type={Type} RecordCount={Count} TraceId={TraceId}",
            result.Id, result.CheckpointType, result.RecordCount, traceId);

        // ── Canonical audit: audit.integrity.checkpoint.generated ─────────────
        // LS-ID-TNT-017-003: Fire-and-observe — the 201 response is never gated
        // on audit publish success.
        LogCheckpointGenerated(caller, result, request, traceId);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<IntegrityCheckpointResponse>.Ok(result, traceId: traceId));
    }

    // ── Private authorization helper ──────────────────────────────────────────

    /// <summary>
    /// Checks that the resolved caller has at least the required minimum scope.
    /// Returns an IActionResult error when the check fails; null when the caller may proceed.
    ///
    /// The caller context is set by <see cref="Middleware.QueryAuthMiddleware"/>
    /// in HttpContext.Items. Falls back to PlatformAdmin when absent (dev/test bypass).
    /// </summary>
    private IActionResult? RequireScope(CallerScope minimum)
    {
        var traceId = TraceIdAccessor.Current();

        var caller = HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
                  && raw is IQueryCallerContext ctx
                     ? ctx
                     : QueryCallerContext.Anonymous();

        if (caller.Scope == CallerScope.Unknown)
        {
            _logger.LogWarning(
                "Checkpoint endpoint denied — caller scope is Unknown. AuthMode={Mode} TraceId={TraceId}",
                caller.AuthMode, traceId);

            return caller.IsAuthenticated
                ? StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(
                        "Your identity could not be mapped to a recognized authorization scope.",
                        traceId: traceId))
                : Unauthorized(ApiResponse<object>.Fail(
                    "Authentication is required to access integrity checkpoints.",
                    traceId: traceId));
        }

        if (caller.Scope < minimum)
        {
            _logger.LogWarning(
                "Checkpoint endpoint denied — insufficient scope. " +
                "CallerScope={Scope} Required={Required} TraceId={TraceId}",
                caller.Scope, minimum, traceId);

            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(
                    $"This endpoint requires {minimum} scope or higher. " +
                    $"Your current scope is {caller.Scope}.",
                    traceId: traceId));
        }

        return null;
    }

    /// <summary>
    /// Emits <c>audit.integrity.checkpoint.generated</c> after a checkpoint is successfully
    /// generated and persisted.
    ///
    /// LS-ID-TNT-017-003: Governance Mutation Audit.
    ///
    /// Fire-and-observe: Task is discarded. The 201 response is returned regardless
    /// of audit publish outcome.
    ///
    /// Scope: Platform — checkpoint generation is PlatformAdmin only.
    /// Actor: Resolved from QueryAuthMiddleware-populated IQueryCallerContext.
    /// Metadata: Includes checkpoint type, time window bounds, record count, and a
    ///   16-character prefix of the AggregateHash for reference. The full hash is
    ///   stored in the IntegrityCheckpoint record and accessible via the list endpoint.
    /// </summary>
    private void LogCheckpointGenerated(
        IQueryCallerContext          caller,
        IntegrityCheckpointResponse  result,
        GenerateCheckpointRequest    request,
        string?                      traceId)
    {
        var hashPrefix = result.AggregateHash.Length >= 16
            ? result.AggregateHash[..16] + "..."
            : result.AggregateHash;

        _ = _ingestionService.IngestSingleAsync(new IngestRequest
        {
            EventType       = "audit.integrity.checkpoint.generated",
            EventCategory   = EventCategory.Compliance,
            SourceSystem    = "audit",
            SourceService   = "integrity-api",
            Visibility      = VisibilityScope.Platform,
            Severity        = SeverityLevel.Notice,
            OccurredAtUtc   = result.CreatedAtUtc,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Platform,
                TenantId  = caller.TenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = caller.UserId ?? "system",
                Type = ActorType.User,
                Name = caller.UserId ?? "system",
            },
            Entity = new AuditEventEntityDto
            {
                Type = "IntegrityCheckpoint",
                Id   = result.Id.ToString(),
            },
            Action      = "CheckpointGenerated",
            Description = $"Integrity checkpoint generated — {result.RecordCount} record(s), type={result.CheckpointType}.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                checkpointId        = result.Id,
                checkpointType      = result.CheckpointType,
                fromRecordedAtUtc   = request.FromRecordedAtUtc,
                toRecordedAtUtc     = request.ToRecordedAtUtc,
                recordCount         = result.RecordCount,
                aggregateHashPrefix = hashPrefix,
                callerScope         = caller.Scope.ToString(),
                callerAuthMode      = caller.AuthMode,
                traceId             = traceId ?? "(none)",
            }),
            CorrelationId  = traceId,
            IdempotencyKey = $"integrity-checkpoint:{result.Id}",
            Tags           = ["governance", "integrity", "compliance-verification"],
        });
    }
}
