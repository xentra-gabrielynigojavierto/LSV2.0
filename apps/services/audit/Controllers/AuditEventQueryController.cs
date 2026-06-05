using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Correlation;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

// Disambiguate from legacy PlatformAuditEventService.DTOs.AuditEventQueryRequest.
using AuditEventQueryRequest   = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;
using AuditEventQueryResponse  = PlatformAuditEventService.DTOs.Query.AuditEventQueryResponse;
using AuditEventRecordResponse = PlatformAuditEventService.DTOs.Query.AuditEventRecordResponse;

// Disambiguate internal ingest DTO from external client DTO aliases.
using IngestRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Query and retrieval endpoints for persisted audit event records.
///
/// Route prefix: /audit
///
/// Authorization: resolved per-request by <see cref="Middleware.QueryAuthMiddleware"/>.
/// Each action calls <see cref="IQueryAuthorizer.Authorize"/> which:
///   1. Validates the caller has sufficient scope for this query.
///   2. Mutates the query in-place to enforce scope constraints (tenant, org, actor, visibility).
///
/// Scoped endpoints (entity, actor, user, tenant, organization) accept additional
/// filter parameters from the query string. Path segments always take precedence
/// over the corresponding query-string field.
///
/// Step 21 hardening:
///   Query parameters are now validated via FluentValidation before authorization.
///   This catches invalid enum values, inverted time ranges, oversized strings, and
///   out-of-range pagination values before they reach the service layer.
///   Validation runs after path params are merged into the query object so that
///   path-derived values (entityType, actorId, etc.) are also length-checked.
/// </summary>
[ApiController]
[Route("audit")]
[Produces("application/json")]
public sealed class AuditEventQueryController : ControllerBase
{
    private readonly IAuditEventQueryService             _queryService;
    private readonly IAuditCorrelationService            _correlationService;
    private readonly IAuditEventIngestionService         _ingestionService;
    private readonly IQueryAuthorizer                    _authorizer;
    private readonly IValidator<AuditEventQueryRequest>  _queryValidator;
    private readonly ILogger<AuditEventQueryController>  _logger;

    public AuditEventQueryController(
        IAuditEventQueryService            queryService,
        IAuditCorrelationService           correlationService,
        IAuditEventIngestionService        ingestionService,
        IQueryAuthorizer                   authorizer,
        IValidator<AuditEventQueryRequest> queryValidator,
        ILogger<AuditEventQueryController> logger)
    {
        _queryService       = queryService;
        _correlationService = correlationService;
        _ingestionService   = ingestionService;
        _authorizer         = authorizer;
        _queryValidator     = queryValidator;
        _logger             = logger;
    }

    // ── GET /audit/events ─────────────────────────────────────────────────────

    /// <summary>
    /// Execute a filtered, paginated query over all accessible audit event records.
    ///
    /// All filter parameters are optional. Multiple filters are AND-ed together.
    /// The caller's scope constrains the result set — callers without PlatformAdmin
    /// scope are restricted to their own tenant's records.
    /// </summary>
    /// <param name="query">Filter and pagination parameters (bound from query string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient for this query.</response>
    [HttpGet("events")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListEvents(
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        _logger.LogDebug(
            "GET /audit/events → TotalCount={Total} TraceId={TraceId}",
            result.TotalCount, traceId);

        LogAuditAccess("GET /audit/events", GetCaller(), result.Items.Count, traceId);
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/events/{auditId} ───────────────────────────────────────────

    /// <summary>
    /// Retrieve a single audit event record by its stable public identifier.
    /// </summary>
    /// <param name="auditId">The platform-assigned AuditId (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Record found.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    /// <response code="404">No record exists with the given AuditId.</response>
    [HttpGet("events/{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(Guid auditId, CancellationToken ct)
    {
        // Authorize with an empty query so the authorizer can mutate it with the
        // caller's scope constraints (TenantId, MaxVisibility, etc.).
        // Those constraints are then forwarded to the service so the single-record
        // fetch is tenant-isolated — closing the cross-tenant bypass.
        var probeQuery = new AuditEventQueryRequest();
        var deny = AuthorizeQuery(probeQuery);
        if (deny is not null) return deny;

        var traceId = TraceIdAccessor.Current();

        // Pass the fully-authorized probeQuery so the repository applies the same
        // ApplyFilters predicate pipeline used by QueryAsync: TenantId, OrganizationId,
        // ActorId, MaxVisibility, and all other constraints the authorizer set.
        var record  = await _queryService.GetByAuditIdAsync(auditId, probeQuery, ct);

        if (record is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                $"Audit event with id '{auditId}' was not found.",
                traceId: traceId));
        }

        LogAuditAccess("GET /audit/events/{auditId}", GetCaller(), 1, traceId, contextId: auditId.ToString());
        return Ok(ApiResponse<AuditEventRecordResponse>.Ok(record, traceId: traceId));
    }

    // ── GET /audit/events/{auditId}/related ──────────────────────────────────

    /// <summary>
    /// Correlation engine: returns audit events related to the given anchor event.
    ///
    /// Applies a deterministic four-tier correlation cascade:
    ///   Tier 1 — CorrelationId exact match       (matchedBy: "correlation_id")
    ///   Tier 2 — SessionId exact match           (matchedBy: "session_id")
    ///   Tier 3 — ActorId + EntityId + ±4 h       (matchedBy: "actor_entity_window")
    ///   Tier 4 — ActorId + ±2 h (fallback only)  (matchedBy: "actor_window")
    ///
    /// Tiers 1–3 are additive; results are merged and deduplicated by AuditId.
    /// Tier 4 runs only when tiers 1–3 collectively yield zero results.
    /// The anchor event itself is excluded from the result set.
    /// All queries are scoped to the caller's effective tenant.
    /// </summary>
    /// <param name="auditId">The stable public AuditId of the anchor event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Correlation complete. Body contains related events (may be empty).</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    /// <response code="404">No anchor event exists with the given AuditId.</response>
    [HttpGet("events/{auditId:guid}/related")]
    [ProducesResponseType(typeof(ApiResponse<RelatedEventsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>),                StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelatedEvents(Guid auditId, CancellationToken ct)
    {
        var probeQuery = new AuditEventQueryRequest();
        var deny = AuthorizeQuery(probeQuery);
        if (deny is not null) return deny;

        var traceId = TraceIdAccessor.Current();
        var caller  = GetCaller();

        var result = await _correlationService.GetRelatedAsync(
            anchorAuditId:  auditId,
            callerTenantId: caller.TenantId,
            ct:             ct);

        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                $"Audit event with id '{auditId}' was not found.",
                traceId: traceId));
        }

        LogAuditAccess(
            "GET /audit/events/{auditId}/related",
            caller,
            result.TotalRelated,
            traceId,
            contextId: auditId.ToString());

        return Ok(ApiResponse<RelatedEventsResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/entity/{entityType}/{entityId} ─────────────────────────────

    /// <summary>
    /// Retrieve all audit events that targeted a specific resource.
    /// Path segments are applied as exact-match filters.
    /// </summary>
    /// <param name="entityType">Resource type (e.g. "User", "Document").</param>
    /// <param name="entityId">Resource identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("entity/{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEntityEvents(
        string entityType,
        string entityId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        // Merge path params first so the validator can check their lengths.
        query.EntityType = entityType;
        query.EntityId   = entityId;

        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        LogAuditAccess("GET /audit/entity/{entityType}/{entityId}", GetCaller(), result.Items.Count, traceId,
            contextId: $"{entityType}/{entityId}");
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/actor/{actorId} ────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events performed by a specific actor.
    /// </summary>
    /// <param name="actorId">The stable actor identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("actor/{actorId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActorEvents(
        string actorId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId = actorId;

        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        LogAuditAccess("GET /audit/actor/{actorId}", GetCaller(), result.Items.Count, traceId, contextId: actorId);
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/user/{userId} ──────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events associated with a specific user.
    /// <c>actorType = User</c> is enforced server-side.
    /// </summary>
    /// <param name="userId">The user's stable identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserEvents(
        string userId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId   = userId;
        query.ActorType = Enums.ActorType.User;

        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        LogAuditAccess("GET /audit/user/{userId}", GetCaller(), result.Items.Count, traceId, contextId: userId);
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/tenant/{tenantId} ──────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific tenant.
    /// For non-PlatformAdmin callers, the authorizer overrides this to the caller's own tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient or tenant mismatch.</response>
    [HttpGet("tenant/{tenantId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenantEvents(
        string tenantId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.TenantId = tenantId;

        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        LogAuditAccess("GET /audit/tenant/{tenantId}", GetCaller(), result.Items.Count, traceId, contextId: tenantId);
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/organization/{organizationId} ──────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific organization.
    /// </summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("organization/{organizationId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationEvents(
        string organizationId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.OrganizationId = organizationId;

        var validationDeny = await ValidateQueryAsync(query, ct);
        if (validationDeny is not null) return validationDeny;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        LogAuditAccess("GET /audit/organization/{organizationId}", GetCaller(), result.Items.Count, traceId,
            contextId: organizationId);
        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs FluentValidation against the query object.
    /// Returns a 400 BadRequest IActionResult on failure; null when the query is valid.
    ///
    /// Call this after merging any path segment values into the query so that
    /// path-derived fields (entityType, actorId, etc.) are also validated.
    /// </summary>
    private async Task<IActionResult?> ValidateQueryAsync(
        AuditEventQueryRequest query,
        CancellationToken      ct)
    {
        var validation = await _queryValidator.ValidateAsync(query, ct);
        if (validation.IsValid) return null;

        var traceId = TraceIdAccessor.Current();

        _logger.LogDebug(
            "Query validation failed. Errors={Errors} TraceId={TraceId}",
            validation.Errors.Select(e => e.ErrorMessage).ToList(),
            traceId);

        return BadRequest(ApiResponse<object>.ValidationFail(
            validation.Errors.Select(e => e.ErrorMessage).ToList(),
            traceId: traceId));
    }

    /// <summary>
    /// Resolves the caller context from HttpContext.Items, calls the authorizer,
    /// and returns an error IActionResult if the query is denied.
    /// Returns null when the caller is authorized — the action may proceed.
    ///
    /// The query is mutated in-place by the authorizer when authorization succeeds.
    /// </summary>
    private IActionResult? AuthorizeQuery(AuditEventQueryRequest query)
    {
        var traceId = TraceIdAccessor.Current();

        // Resolve caller from HttpContext.Items (set by QueryAuthMiddleware).
        // Fall back to Anonymous if not present (e.g. tests that bypass middleware).
        var caller = HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
            && raw is IQueryCallerContext ctx
                ? ctx
                : QueryCallerContext.Anonymous();

        var result = _authorizer.Authorize(caller, query);

        if (result.IsAuthorized) return null;

        _logger.LogWarning(
            "Query access denied. Scope={Scope} StatusCode={Status} Reason={Reason} TraceId={TraceId}",
            caller.Scope, result.StatusCode, result.DenialReason, traceId);

        return result.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                Unauthorized(ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
            _ =>
                StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
        };
    }

    /// <summary>
    /// Returns the caller context for the current request.
    /// Reads from HttpContext.Items (populated by QueryAuthMiddleware).
    /// Falls back to Anonymous when middleware is bypassed (tests, health checks).
    /// </summary>
    private IQueryCallerContext GetCaller() =>
        HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
            && raw is IQueryCallerContext ctx
                ? ctx
                : QueryCallerContext.Anonymous();

    /// <summary>
    /// Emits a structured "audit log accessed" entry at Information level AND
    /// ingests a canonical <c>audit.log.accessed</c> event into the audit store.
    ///
    /// HIPAA §164.312(b): access to audit logs is itself an auditable event.
    ///
    /// Recursion safety guarantee:
    ///   This method calls <see cref="IAuditEventIngestionService.IngestSingleAsync"/>
    ///   which writes directly to the database repository. The ingestion pipeline never
    ///   calls any query endpoint — there is no call cycle. The fire-and-observe pattern
    ///   (discarded Task) ensures this never gates the query response.
    ///
    /// Suppression: audit.log.accessed events are themselves never re-audited.
    ///   The ingest pipeline does not trigger LogAuditAccess for its own writes.
    ///   This is enforced architecturally: the ingestion path does not call this controller.
    /// </summary>
    private void LogAuditAccess(
        string              action,
        IQueryCallerContext caller,
        int                 recordsAccessed,
        string?             traceId,
        string?             contextId = null)
    {
        _logger.LogInformation(
            "AUDIT_LOG_ACCESSED: Action={Action} UserId={UserId} TenantId={TenantId} " +
            "Scope={Scope} AuthMode={AuthMode} RecordsAccessed={Count} " +
            "ContextId={ContextId} TraceId={TraceId}",
            action,
            caller.UserId      ?? "(anonymous)",
            caller.TenantId    ?? "(platform)",
            caller.Scope,
            caller.AuthMode,
            recordsAccessed,
            contextId          ?? "(none)",
            traceId            ?? "(no-trace)");

        // Canonical audit-of-audit: emit to the persistent audit store.
        // Fire-and-observe — the Task is intentionally discarded.
        // This event is stored exactly like any other ingest event (same pipeline, same table),
        // but it will not trigger a further LogAuditAccess call because the ingest path
        // never calls the query controller.
        var now = DateTimeOffset.UtcNow;
        _ = _ingestionService.IngestSingleAsync(new IngestRequest
        {
            EventType       = "audit.log.accessed",
            EventCategory   = EventCategory.Access,
            SourceSystem    = "audit",
            SourceService   = "audit-query-api",
            Visibility      = VisibilityScope.Platform,
            Severity        = SeverityLevel.Info,
            OccurredAtUtc   = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = caller.TenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = caller.UserId,
                Type = ActorType.User,
                Name = caller.UserId ?? "(anonymous)",
            },
            Action      = action,
            Description = $"Audit log queried: {action} — {recordsAccessed} record(s) accessed.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                endpoint       = action,
                recordsAccessed,
                contextId      = contextId ?? "(none)",
                callerScope    = caller.Scope.ToString(),
                callerAuthMode = caller.AuthMode,
                traceId        = traceId ?? "(none)",
            }),
            CorrelationId  = traceId,
            IdempotencyKey = $"audit-access:{traceId ?? Guid.NewGuid().ToString("N")}:{action}",
            Tags           = ["audit-of-audit", "access"],
        });
    }
}
