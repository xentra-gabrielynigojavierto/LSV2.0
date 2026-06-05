using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.DTOs.LegalHold;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

using IngestRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// API endpoints for placing and releasing legal holds on audit event records.
///
/// Route prefix: /audit/legal-holds
///
/// Legal holds prevent the retention pipeline from archiving or deleting held records.
/// This controller is intended for use by compliance officers and legal staff.
///
/// Authorization:
///   All endpoints require PlatformAdmin or ComplianceOfficer scope.
///   In Mode=Bearer, these are resolved from JWT claims.
///   In Mode=None (dev), all callers have PlatformAdmin scope by default.
///
/// HIPAA alignment:
///   All hold creation and release operations are logged at WARNING level for
///   compliance audit trail purposes. The log lines include HoldId, AuditId,
///   LegalAuthority, and the identity of the requester.
///
/// LS-ID-TNT-017-003:
///   Legal hold create and release now also emit canonical compliance audit events
///   (<c>audit.legal_hold.created</c>, <c>audit.legal_hold.released</c>) via the
///   centralized ingestion pipeline. Fire-and-observe; mutation never gated on audit publish.
/// </summary>
[ApiController]
[Route("audit/legal-holds")]
[Produces("application/json")]
public sealed class LegalHoldController : ControllerBase
{
    private readonly ILegalHoldService                _holdService;
    private readonly IAuditEventIngestionService      _ingestionService;
    private readonly ILogger<LegalHoldController>     _logger;

    public LegalHoldController(
        ILegalHoldService             holdService,
        IAuditEventIngestionService   ingestionService,
        ILogger<LegalHoldController>  logger)
    {
        _holdService      = holdService;
        _ingestionService = ingestionService;
        _logger           = logger;
    }

    // ── POST /audit/legal-holds/{auditId} ─────────────────────────────────────

    /// <summary>
    /// Place a legal hold on an audit event record.
    ///
    /// The hold prevents the retention pipeline from archiving or deleting the record
    /// until the hold is explicitly released.
    /// </summary>
    /// <param name="auditId">The AuditId of the record to hold.</param>
    /// <param name="request">Legal hold details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Hold created successfully.</response>
    /// <response code="404">Audit record not found.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LegalHoldResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateHold(
        Guid                    auditId,
        [FromBody] CreateLegalHoldRequest request,
        CancellationToken       ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request"));

        var traceId = TraceIdAccessor.Current();
        var caller  = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext
                      ?? QueryCallerContext.Anonymous();

        try
        {
            var userId = ResolveCallerId();
            var hold   = await _holdService.CreateHoldAsync(auditId, userId, request, ct);

            // ── Canonical audit: audit.legal_hold.created ─────────────────────
            // LS-ID-TNT-017-003: Fire-and-observe — the 201 response is never gated
            // on audit publish success.
            LogLegalHoldCreated(caller, hold, traceId);

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<LegalHoldResponse>.Ok(hold));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("LegalHold creation failed: {Reason}", ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ── POST /audit/legal-holds/{holdId}/release ──────────────────────────────

    /// <summary>
    /// Release an active legal hold.
    ///
    /// After release, the record becomes eligible for the normal retention lifecycle.
    /// </summary>
    /// <param name="holdId">The HoldId of the hold to release.</param>
    /// <param name="request">Optional release notes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Hold released successfully.</response>
    /// <response code="404">Hold not found.</response>
    /// <response code="409">Hold is already released.</response>
    [HttpPost("{holdId:guid}/release")]
    [ProducesResponseType(typeof(ApiResponse<LegalHoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReleaseHold(
        Guid holdId,
        [FromBody] ReleaseLegalHoldRequest request,
        CancellationToken ct)
    {
        var traceId = TraceIdAccessor.Current();
        var caller  = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext
                      ?? QueryCallerContext.Anonymous();

        try
        {
            var userId = ResolveCallerId();
            var hold   = await _holdService.ReleaseHoldAsync(holdId, userId, request, ct);

            // ── Canonical audit: audit.legal_hold.released ────────────────────
            // LS-ID-TNT-017-003: Fire-and-observe — the 200 response is never gated
            // on audit publish success.
            LogLegalHoldReleased(caller, hold, traceId);

            return Ok(ApiResponse<LegalHoldResponse>.Ok(hold));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already released"))
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("LegalHold release failed: {Reason}", ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ── GET /audit/legal-holds/record/{auditId} ───────────────────────────────

    /// <summary>
    /// List all holds (active and released) for an audit event record.
    /// </summary>
    /// <param name="auditId">The AuditId of the record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List of holds for the record (may be empty).</response>
    [HttpGet("record/{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LegalHoldResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByAuditId(Guid auditId, CancellationToken ct)
    {
        var holds = await _holdService.ListByAuditIdAsync(auditId, ct);
        return Ok(ApiResponse<IReadOnlyList<LegalHoldResponse>>.Ok(holds));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the caller identity. In Bearer mode this is the JWT sub claim.
    /// In None mode (dev), returns a placeholder identity string.
    /// </summary>
    private string ResolveCallerId()
    {
        // Sub claim (OpenID Connect standard for user identity)
        var sub = User.FindFirst("sub")?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(sub))
            return sub;

        // Fallback for dev mode (Mode=None — no JWT)
        return "system:dev-caller";
    }

    /// <summary>
    /// Emits <c>audit.legal_hold.created</c> after a legal hold is successfully placed.
    ///
    /// LS-ID-TNT-017-003: Governance Mutation Audit.
    ///
    /// Fire-and-observe: Task is discarded. The 201 response is returned regardless
    /// of audit publish outcome.
    ///
    /// Scope: Platform — legal hold operations are PlatformAdmin/ComplianceOfficer actions.
    /// Actor: Resolved from QueryAuthMiddleware-populated IQueryCallerContext in HttpContext.Items.
    /// Metadata: LegalAuthority (structured reference) included. Request Notes excluded
    ///   (may contain free-form legal content inappropriate for audit metadata).
    /// </summary>
    private void LogLegalHoldCreated(
        IQueryCallerContext caller,
        LegalHoldResponse   hold,
        string?             traceId)
    {
        _logger.LogWarning(
            "LegalHold CREATED: HoldId={HoldId} AuditId={AuditId} Authority={Authority} " +
            "HeldBy={UserId} CallerScope={Scope} TraceId={TraceId}",
            hold.HoldId, hold.AuditId, hold.LegalAuthority,
            hold.HeldByUserId, caller.Scope, traceId ?? "(no-trace)");

        _ = _ingestionService.IngestSingleAsync(new IngestRequest
        {
            EventType       = "audit.legal_hold.created",
            EventCategory   = EventCategory.Compliance,
            SourceSystem    = "audit",
            SourceService   = "legal-hold-api",
            Visibility      = VisibilityScope.Platform,
            Severity        = SeverityLevel.Warn,
            OccurredAtUtc   = hold.HeldAtUtc,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Platform,
                TenantId  = caller.TenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = caller.UserId ?? hold.HeldByUserId,
                Type = ActorType.User,
                Name = caller.UserId ?? hold.HeldByUserId,
            },
            Entity = new AuditEventEntityDto
            {
                Type = "LegalHold",
                Id   = hold.HoldId.ToString(),
            },
            Action      = "LegalHoldPlaced",
            Description = $"Legal hold placed on audit record {hold.AuditId} — authority: {hold.LegalAuthority}.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                holdId        = hold.HoldId,
                auditId       = hold.AuditId,
                legalAuthority= hold.LegalAuthority,
                heldByUserId  = hold.HeldByUserId,
                heldAtUtc     = hold.HeldAtUtc,
                callerScope   = caller.Scope.ToString(),
                callerAuthMode= caller.AuthMode,
                traceId       = traceId ?? "(none)",
            }),
            CorrelationId  = traceId,
            IdempotencyKey = $"legal-hold-created:{hold.HoldId}",
            Tags           = ["governance", "legal-hold", "retention-control"],
        });
    }

    /// <summary>
    /// Emits <c>audit.legal_hold.released</c> after a legal hold is successfully released.
    ///
    /// LS-ID-TNT-017-003: Governance Mutation Audit.
    ///
    /// Fire-and-observe: Task is discarded. The 200 response is returned regardless
    /// of audit publish outcome.
    /// </summary>
    private void LogLegalHoldReleased(
        IQueryCallerContext caller,
        LegalHoldResponse   hold,
        string?             traceId)
    {
        _logger.LogWarning(
            "LegalHold RELEASED: HoldId={HoldId} AuditId={AuditId} Authority={Authority} " +
            "ReleasedBy={ReleasedBy} ReleasedAt={ReleasedAt} CallerScope={Scope} TraceId={TraceId}",
            hold.HoldId, hold.AuditId, hold.LegalAuthority,
            hold.ReleasedByUserId, hold.ReleasedAtUtc, caller.Scope, traceId ?? "(no-trace)");

        _ = _ingestionService.IngestSingleAsync(new IngestRequest
        {
            EventType       = "audit.legal_hold.released",
            EventCategory   = EventCategory.Compliance,
            SourceSystem    = "audit",
            SourceService   = "legal-hold-api",
            Visibility      = VisibilityScope.Platform,
            Severity        = SeverityLevel.Warn,
            OccurredAtUtc   = hold.ReleasedAtUtc ?? DateTimeOffset.UtcNow,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Platform,
                TenantId  = caller.TenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = caller.UserId ?? hold.ReleasedByUserId ?? "(unknown)",
                Type = ActorType.User,
                Name = caller.UserId ?? hold.ReleasedByUserId ?? "(unknown)",
            },
            Entity = new AuditEventEntityDto
            {
                Type = "LegalHold",
                Id   = hold.HoldId.ToString(),
            },
            Action      = "LegalHoldReleased",
            Description = $"Legal hold {hold.HoldId} released on audit record {hold.AuditId} — authority: {hold.LegalAuthority}.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                holdId          = hold.HoldId,
                auditId         = hold.AuditId,
                legalAuthority  = hold.LegalAuthority,
                releasedByUserId= hold.ReleasedByUserId,
                releasedAtUtc   = hold.ReleasedAtUtc,
                callerScope     = caller.Scope.ToString(),
                callerAuthMode  = caller.AuthMode,
                traceId         = traceId ?? "(none)",
            }),
            CorrelationId  = traceId,
            IdempotencyKey = $"legal-hold-released:{hold.HoldId}",
            Tags           = ["governance", "legal-hold", "retention-control"],
        });
    }
}
