using System.Security.Claims;
using BuildingBlocks.Authorization;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// E17 — read-only admin inspection of <see cref="OutboxMessage"/> rows
/// and a governed manual retry for Failed / DeadLettered items.
///
/// <para>
/// Authorization: <see cref="Policies.PlatformOrTenantAdmin"/>. Platform
/// admins can see rows across all tenants (optionally narrowed by
/// <c>?tenantId=</c>); tenant admins see only their own tenant's rows.
/// Out-of-scope rows return 404 — existence in another tenant is
/// intentionally invisible.
/// </para>
///
/// <para>
/// The outbox entity has no per-tenant EF query filter by design (the
/// <c>OutboxProcessor</c> runs in a null-tenant scope). Every query here
/// calls <c>IgnoreQueryFilters()</c> and re-applies the appropriate tenant
/// predicate explicitly, mirroring the pattern in
/// <see cref="AdminWorkflowInstancesController"/>.
/// </para>
///
/// <para>
/// The manual retry endpoint resets the row to <c>Pending</c> with a
/// zeroed <c>AttemptCount</c> so it re-enters the processor's normal
/// backoff schedule. Audit is written directly via <see cref="IAuditAdapter"/>
/// (not through the outbox) to avoid a circular outbox-row mutation.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/outbox")]
[Authorize(Policy = Policies.PlatformOrTenantAdmin)]
public class AdminOutboxController : ControllerBase
{
    private const int DefaultPageSize  = 20;
    private const int MaxPageSize      = 100;
    private const int MaxReasonLength  = 1000;

    // Payload preview: the first N chars of PayloadJson are surfaced in
    // the detail view so operators can recognise the event context without
    // a raw JSON dump. Sensitive fields (reasons, email addresses embedded
    // by admin-action handlers) are potentially present, so the truncation
    // acts as a minimal guard. Full payload sanitization is deferred.
    private const int PayloadPreviewMaxChars = 300;

    private readonly IFlowDbContext  _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditAdapter   _audit;
    private readonly ILogger<AdminOutboxController> _logger;

    public AdminOutboxController(
        IFlowDbContext  db,
        ITenantProvider tenantProvider,
        IAuditAdapter   audit,
        ILogger<AdminOutboxController> logger)
    {
        _db             = db;
        _tenantProvider = tenantProvider;
        _audit          = audit;
        _logger         = logger;
    }

    // ── List ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of outbox items, optionally filtered by
    /// status, event type, tenant, workflow instance id, or exact outbox id.
    ///
    /// <para>
    /// Ordering: newest-first (<c>CreatedAt DESC</c>) so recently failed /
    /// dead-lettered items surface at the top for triage. Stable and
    /// predictable regardless of processing activity.
    /// </para>
    ///
    /// <para>
    /// <c>?status=DeadLettered</c> is the recommended dead-letter preset.
    /// <c>?search=&lt;guid&gt;</c> narrows to a single exact outbox id for
    /// copy-paste lookups.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? eventType,
        [FromQuery] string? tenantId,
        [FromQuery] Guid?   workflowInstanceId,
        [FromQuery] Guid?   search,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = DefaultPageSize,
        CancellationToken   ct       = default)
    {
        var p  = page < 1 ? 1 : page;
        var ps = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        // Bypass the per-tenant EF query filter; apply tenant scoping in code.
        IQueryable<OutboxMessage> q = _db.OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters();

        if (isPlatformAdmin)
        {
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var t = tenantId.Trim().ToLowerInvariant();
                q = q.Where(o => o.TenantId == t);
            }
        }
        else
        {
            string callerTid;
            try { callerTid = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(callerTid)) return Forbid();
            q = q.Where(o => o.TenantId == callerTid);
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(o => o.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(eventType))
            q = q.Where(o => o.EventType == eventType.Trim());

        if (workflowInstanceId.HasValue)
            q = q.Where(o => o.WorkflowInstanceId == workflowInstanceId.Value);

        if (search.HasValue)
            q = q.Where(o => o.Id == search.Value);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(o => new AdminOutboxListItem
            {
                Id                 = o.Id,
                TenantId           = o.TenantId,
                WorkflowInstanceId = o.WorkflowInstanceId,
                EventType          = o.EventType,
                Status             = o.Status,
                AttemptCount       = o.AttemptCount,
                CreatedAt          = o.CreatedAt,
                UpdatedAt          = o.UpdatedAt,
                NextAttemptAt      = o.NextAttemptAt,
                ProcessedAt        = o.ProcessedAt,
                // Truncate LastError in the list view so the table row
                // stays readable; full error is available in detail.
                LastError          = o.LastError != null
                    ? (o.LastError.Length > 200 ? o.LastError.Substring(0, 200) : o.LastError)
                    : null,
            })
            .ToListAsync(ct);

        _logger.LogInformation(
            "AdminOutbox.List platformAdmin={IsPlatformAdmin} count={Count} total={Total} status={Status} eventType={EventType} tenant={TenantId}",
            isPlatformAdmin, rows.Count, total, status, eventType, tenantId);

        return Ok(new AdminOutboxListResponse
        {
            Items      = rows,
            TotalCount = total,
            Page       = p,
            PageSize   = ps,
        });
    }

    // ── Summary ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a lightweight count of outbox items grouped by status.
    /// Used to populate the summary cards at the top of the outbox ops page.
    ///
    /// <para>
    /// Tenant scoping rules are identical to <see cref="List"/>. Not cached
    /// at the BFF boundary so operators always see the current state.
    /// </para>
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        IQueryable<OutboxMessage> q = _db.OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters();

        if (!isPlatformAdmin)
        {
            string callerTid;
            try { callerTid = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(callerTid)) return Forbid();
            q = q.Where(o => o.TenantId == callerTid);
        }

        // Single aggregation query; EF translates GROUP BY on the
        // varchar(16) Status column — fine under expected row counts.
        var counts = await q
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(string s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        _logger.LogInformation(
            "AdminOutbox.Summary platformAdmin={IsPlatformAdmin}", isPlatformAdmin);

        return Ok(new AdminOutboxSummary
        {
            PendingCount      = Get(OutboxStatus.Pending),
            ProcessingCount   = Get(OutboxStatus.Processing),
            FailedCount       = Get(OutboxStatus.Failed),
            DeadLetteredCount = Get(OutboxStatus.DeadLettered),
            SucceededCount    = Get(OutboxStatus.Succeeded),
        });
    }

    // ── Detail ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns full detail for a single outbox item, including the full
    /// <c>LastError</c> string, a truncated payload summary, and an
    /// <c>isRetryEligible</c> flag the drawer uses to show/hide the
    /// retry button.
    ///
    /// <para>
    /// Returns 404 when the item does not exist or belongs to a different
    /// tenant than the calling TenantAdmin (existence not leaked).
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        string? scopeTenantId = null;
        if (!isPlatformAdmin)
        {
            try { scopeTenantId = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(scopeTenantId)) return Forbid();
        }

        var row = await _db.OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (row is null) return NotFound();
        if (!isPlatformAdmin && row.TenantId != scopeTenantId) return NotFound();

        // Truncated payload preview. Intentionally minimal to avoid
        // surfacing embedded PII (admin-action reasons, email addresses,
        // correlation keys from domain entities). Full payload sanitization
        // is deferred to a later phase.
        var payloadSummary = row.PayloadJson.Length > PayloadPreviewMaxChars
            ? row.PayloadJson[..PayloadPreviewMaxChars] + "…"
            : row.PayloadJson;

        _logger.LogInformation(
            "AdminOutbox.GetById id={OutboxId} platformAdmin={IsPlatformAdmin} tenant={TenantId}",
            id, isPlatformAdmin, row.TenantId);

        return Ok(new AdminOutboxDetail
        {
            Id                 = row.Id,
            TenantId           = row.TenantId,
            WorkflowInstanceId = row.WorkflowInstanceId,
            EventType          = row.EventType,
            Status             = row.Status,
            AttemptCount       = row.AttemptCount,
            CreatedAt          = row.CreatedAt,
            UpdatedAt          = row.UpdatedAt,
            NextAttemptAt      = row.NextAttemptAt,
            ProcessedAt        = row.ProcessedAt,
            LastError          = row.LastError,
            PayloadSummary     = payloadSummary,
            IsRetryEligible    = row.Status == OutboxStatus.Failed
                              || row.Status == OutboxStatus.DeadLettered,
        });
    }

    // ── Manual Retry ─────────────────────────────────────────────────────

    /// <summary>
    /// E17 — governed manual retry for a <c>Failed</c> or
    /// <c>DeadLettered</c> outbox item.
    ///
    /// <para>
    /// The mutation resets the row to <c>Pending</c> with
    /// <c>AttemptCount=0</c> and <c>NextAttemptAt=now</c> so the existing
    /// <c>OutboxProcessor</c> picks it up on its next poll tick. No second
    /// retry mechanism is introduced; the only change is that the item
    /// re-enters the processor's normal backoff schedule with a fresh
    /// attempt budget (zeroing the counter is the operator's explicit
    /// intent after automatic retry was exhausted).
    /// </para>
    ///
    /// <para>
    /// Idempotency guarantee: each outbox payload already carries the
    /// row id as a correlation token for downstream dedupe
    /// (see <c>OutboxProcessor</c> comments). Re-dispatching the same
    /// row is therefore safe.
    /// </para>
    ///
    /// <para>
    /// Audit is written directly via <see cref="IAuditAdapter"/> rather
    /// than through the outbox to avoid a circular outbox-row mutation.
    /// The adapter is documented fire-and-forget safe; an audit-pipeline
    /// outage will not fail the retry action.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid id,
        [FromBody] AdminOutboxRetryRequest? body,
        CancellationToken ct)
    {
        // Reason validation first — fail fast before any DB access.
        var reason = (body?.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title:      "reason_required",
                detail:     "An admin action reason is required.");
        }
        if (reason.Length > MaxReasonLength) reason = reason[..MaxReasonLength];

        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        string? scopeTenantId = null;
        if (!isPlatformAdmin)
        {
            try { scopeTenantId = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(scopeTenantId)) return Forbid();
        }

        // Load tracked so EF can persist the mutation.
        var row = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (row is null) return NotFound();
        if (!isPlatformAdmin && row.TenantId != scopeTenantId) return NotFound();

        // Eligibility gate: only Failed and DeadLettered items are
        // retryable. Pending/Processing are already being served by the
        // processor; Succeeded items must never be re-dispatched.
        if (row.Status != OutboxStatus.Failed && row.Status != OutboxStatus.DeadLettered)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title:      "not_retryable",
                detail:     $"Manual retry is only allowed for Failed or DeadLettered outbox items. Current status: {row.Status}.");
        }

        var previousStatus = row.Status;
        var now            = DateTime.UtcNow;
        var performedBy    = ResolvePerformedBy();

        row.Status        = OutboxStatus.Pending;
        row.AttemptCount  = 0;
        row.NextAttemptAt = now;
        row.LastError     = null;
        row.UpdatedAt     = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title:      "concurrent_state_change",
                detail:     "Another writer modified this outbox item while the retry was being applied. Please reload and try again.");
        }

        // Audit: fire-and-forget via IAuditAdapter. Using the outbox here
        // would be circular (we're mutating an outbox row, so we can't enqueue
        // another outbox row to audit it without creating confusion). The
        // direct adapter call is the correct pattern for this case.
        try
        {
            await _audit.WriteEventAsync(new AuditEvent(
                Action:      "outbox.manual_retry",
                EntityType:  "OutboxMessage",
                EntityId:    id.ToString(),
                TenantId:    row.TenantId,
                UserId:      performedBy,
                Description: $"Admin manually retried outbox item. PreviousStatus={previousStatus} EventType={row.EventType}",
                Metadata: new Dictionary<string, string?>
                {
                    ["outboxId"]           = id.ToString(),
                    ["eventType"]          = row.EventType,
                    ["workflowInstanceId"] = row.WorkflowInstanceId?.ToString(),
                    ["previousStatus"]     = previousStatus,
                    ["newStatus"]          = OutboxStatus.Pending,
                    ["reason"]             = reason,
                    ["performedBy"]        = performedBy,
                    ["isPlatformAdmin"]    = isPlatformAdmin.ToString(),
                },
                OccurredAtUtc: now), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AdminOutbox.Retry — audit emit failed (fire-and-forget). id={OutboxId}", id);
        }

        _logger.LogInformation(
            "AdminOutbox.Retry id={OutboxId} eventType={EventType} tenant={TenantId} {Previous}->Pending performedBy={PerformedBy}",
            id, row.EventType, row.TenantId, previousStatus, performedBy);

        return Ok(new AdminOutboxRetryResult
        {
            OutboxId       = id,
            EventType      = row.EventType,
            PreviousStatus = previousStatus,
            NewStatus      = OutboxStatus.Pending,
            PerformedBy    = performedBy,
            Timestamp      = now,
            Reason         = reason,
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Best-effort actor identifier for audit. Tries the common JWT subject
    /// claims in priority order; falls back to a sentinel so an audit row
    /// is never produced with an empty performedBy.
    /// </summary>
    private string ResolvePerformedBy()
    {
        var u = User;
        return u.FindFirstValue("preferred_username")
            ?? u.FindFirstValue("email")
            ?? u.FindFirstValue(ClaimTypes.Email)
            ?? u.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? u.FindFirstValue("sub")
            ?? u.Identity?.Name
            ?? "unknown_admin";
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>
/// E17 — projection for a single row in the outbox list view. <c>LastError</c>
/// is truncated to 200 chars to keep table rows readable; the full error is
/// available via <see cref="AdminOutboxDetail"/>.
/// </summary>
public sealed record AdminOutboxListItem
{
    public Guid    Id                 { get; init; }
    public string  TenantId           { get; init; } = string.Empty;
    public Guid?   WorkflowInstanceId { get; init; }
    public string  EventType          { get; init; } = string.Empty;
    public string  Status             { get; init; } = string.Empty;
    public int     AttemptCount       { get; init; }
    public DateTime  CreatedAt        { get; init; }
    public DateTime? UpdatedAt        { get; init; }
    public DateTime  NextAttemptAt    { get; init; }
    public DateTime? ProcessedAt      { get; init; }
    /// <summary>Truncated to 200 chars in this projection.</summary>
    public string? LastError          { get; init; }
}

/// <summary>
/// E17 — full detail for a single outbox item, including the complete
/// <c>LastError</c>, a truncated <c>PayloadSummary</c>, and the retry
/// eligibility flag.
/// </summary>
public sealed record AdminOutboxDetail
{
    public Guid    Id                 { get; init; }
    public string  TenantId           { get; init; } = string.Empty;
    public Guid?   WorkflowInstanceId { get; init; }
    public string  EventType          { get; init; } = string.Empty;
    public string  Status             { get; init; } = string.Empty;
    public int     AttemptCount       { get; init; }
    public DateTime  CreatedAt        { get; init; }
    public DateTime? UpdatedAt        { get; init; }
    public DateTime  NextAttemptAt    { get; init; }
    public DateTime? ProcessedAt      { get; init; }
    public string? LastError          { get; init; }
    /// <summary>First 300 chars of PayloadJson, truncated with "…".</summary>
    public string? PayloadSummary     { get; init; }
    /// <summary>True iff Status is Failed or DeadLettered.</summary>
    public bool    IsRetryEligible    { get; init; }
}

/// <summary>
/// E17 — lightweight grouped counts for the summary card row on the
/// outbox ops page.
/// </summary>
public sealed record AdminOutboxSummary
{
    public int PendingCount      { get; init; }
    public int ProcessingCount   { get; init; }
    public int FailedCount       { get; init; }
    public int DeadLetteredCount { get; init; }
    public int SucceededCount    { get; init; }
}

/// <summary>E17 — paged list response envelope.</summary>
public sealed record AdminOutboxListResponse
{
    public List<AdminOutboxListItem> Items     { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page       { get; init; }
    public int PageSize   { get; init; }
}

/// <summary>E17 — request body for the manual retry endpoint.</summary>
public sealed record AdminOutboxRetryRequest
{
    public string? Reason { get; init; }
}

/// <summary>
/// E17 — structured response from the manual retry endpoint. Mirrors the
/// shape of <see cref="AdminWorkflowActionResult"/> so the CC drawer can
/// use a consistent pattern.
/// </summary>
public sealed record AdminOutboxRetryResult
{
    public Guid     OutboxId       { get; init; }
    public string   EventType      { get; init; } = string.Empty;
    public string   PreviousStatus { get; init; } = string.Empty;
    public string   NewStatus      { get; init; } = string.Empty;
    public string   PerformedBy    { get; init; } = string.Empty;
    public DateTime Timestamp      { get; init; }
    public string   Reason         { get; init; } = string.Empty;
}
