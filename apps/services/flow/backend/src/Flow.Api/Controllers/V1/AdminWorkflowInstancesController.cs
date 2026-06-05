using System.Security.Claims;
using BuildingBlocks.Authorization;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Interfaces;
using Flow.Application.Outbox;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// E9.1 — read-only admin listing of <see cref="WorkflowInstance"/> rows for
/// the Control Center cross-product workflow operations view.
///
/// <para>
/// Authorization: <see cref="Policies.PlatformOrTenantAdmin"/>. Cross-tenant
/// visibility is granted only to <see cref="Roles.PlatformAdmin"/>; tenant
/// admins are scoped to their own tenant on the server side regardless of
/// any inbound query parameter.
/// </para>
///
/// <para>
/// The handler bypasses the per-tenant EF query filter via
/// <c>IgnoreQueryFilters()</c> and re-applies the appropriate tenant
/// predicate explicitly in code so a PlatformAdmin can see all rows while
/// any other admin sees only their own tenant. This endpoint is read-only;
/// execution / mutation surfaces remain on the existing controllers.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/workflow-instances")]
[Authorize(Policy = Policies.PlatformOrTenantAdmin)]
public class AdminWorkflowInstancesController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// E9.3 — default "stale" threshold for the Stuck classification when
    /// the caller does not pass <c>staleThresholdHours</c>. 24h is the
    /// safe baseline: any Active/Pending workflow that has not been
    /// touched (UpdatedAt ?? CreatedAt) within the last day is flagged.
    /// </summary>
    private const int DefaultStaleThresholdHours = 24;
    private const int MinStaleThresholdHours     = 1;
    private const int MaxStaleThresholdHours     = 24 * 30; // 30d ceiling

    /// <summary>
    /// E9.3 — supported classification labels for the exception view.
    /// Kept as constants so server-side filtering and per-row tagging
    /// agree on the exact spelling. Multiple labels can apply to a single
    /// row (e.g. a Failed workflow that also has a lastErrorMessage).
    /// </summary>
    private const string ClassFailed       = "Failed";
    private const string ClassCancelled    = "Cancelled";
    private const string ClassStuck        = "Stuck";
    private const string ClassErrorPresent = "ErrorPresent";

    /// <summary>
    /// E10.1 — admin-action constants. Kept symmetrical with the
    /// Control Center client + audit metadata so triage UIs and audit
    /// log queries share an exact spelling.
    /// </summary>
    private const string ActionRetry          = "retry";
    private const string ActionForceComplete  = "force_complete";
    private const string ActionCancel         = "cancel";
    private const int    MaxReasonLength      = 1000;

    private readonly IFlowDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditAdapter _audit;
    private readonly IAuditQueryAdapter _auditQuery;
    private readonly IOutboxWriter _outbox;
    private readonly IWorkflowTaskFromWorkflowFactory _taskFactory;
    private readonly ILogger<AdminWorkflowInstancesController> _logger;

    public AdminWorkflowInstancesController(
        IFlowDbContext db,
        ITenantProvider tenantProvider,
        IAuditAdapter audit,
        IAuditQueryAdapter auditQuery,
        IOutboxWriter outbox,
        IWorkflowTaskFromWorkflowFactory taskFactory,
        ILogger<AdminWorkflowInstancesController> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _audit = audit;
        _auditQuery = auditQuery;
        _outbox = outbox;
        _taskFactory = taskFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? productKey,
        [FromQuery] string? status,
        [FromQuery] string? tenantId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        // ── E9.3 — exception / stuck triage filters ──────────────────────
        // All three are optional. When omitted, the endpoint returns the
        // identical response shape and ordering as the E9.1 surface so
        // existing callers are unaffected.
        [FromQuery] bool exceptionOnly = false,
        [FromQuery] string? classification = null,
        [FromQuery] int? staleThresholdHours = null,
        CancellationToken ct = default)
    {
        var p  = page < 1 ? 1 : page;
        var ps = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        // Clamp the stale threshold and compute a single cut-off timestamp
        // so the SQL predicate stays index-friendly (no per-row date math).
        var staleHours = staleThresholdHours
            is int sh && sh >= MinStaleThresholdHours
            ? Math.Min(sh, MaxStaleThresholdHours)
            : DefaultStaleThresholdHours;
        var staleCutoff = DateTime.UtcNow.AddHours(-staleHours);

        // Normalise an explicit classification filter (UI sends one of the
        // constants below). An unknown value is silently ignored so a
        // bad/legacy URL still returns a stable response.
        var classFilter = classification?.Trim();
        if (!string.IsNullOrEmpty(classFilter)
            && classFilter != ClassFailed
            && classFilter != ClassCancelled
            && classFilter != ClassStuck
            && classFilter != ClassErrorPresent)
        {
            classFilter = null;
        }

        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        // Bypass the per-tenant EF query filter and apply tenant scoping
        // explicitly: PlatformAdmin sees everything (optionally narrowed by
        // an explicit tenantId param), TenantAdmin always sees only their
        // own tenant — the inbound tenantId param is ignored for them.
        IQueryable<WorkflowInstance> q = _db.WorkflowInstances
            .AsNoTracking()
            .IgnoreQueryFilters();

        if (isPlatformAdmin)
        {
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var t = tenantId.Trim().ToLowerInvariant();
                q = q.Where(w => w.TenantId == t);
            }
        }
        else
        {
            string callerTid;
            try { callerTid = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(callerTid)) return Forbid();
            q = q.Where(w => w.TenantId == callerTid);
        }

        if (!string.IsNullOrWhiteSpace(productKey))
        {
            var pk = productKey.Trim();
            q = q.Where(w => w.ProductKey == pk);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            q = q.Where(w => w.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term    = search.Trim();
            var pattern = $"%{term}%";
            // Lightweight contains-style search across the fields an
            // operator is most likely to paste in: instance id, correlation
            // key, and current step key. MySQL's default collation is
            // case-insensitive, so a plain LIKE is sufficient — using
            // EF.Functions.Like keeps the predicate translatable.
            q = q.Where(w =>
                (w.CorrelationKey != null && EF.Functions.Like(w.CorrelationKey, pattern)) ||
                (w.CurrentStepKey != null && EF.Functions.Like(w.CurrentStepKey, pattern)) ||
                EF.Functions.Like(w.Id.ToString(), pattern));
        }

        // ── E9.3 — exception / stuck server-side narrowing ───────────────
        //
        // RULES (deterministic; surfaced verbatim as per-row classifications):
        //   Failed       : Status == "Failed"
        //   Cancelled    : Status == "Cancelled"
        //   Stuck        : Status in ("Active","Pending") AND
        //                  (UpdatedAt ?? CreatedAt) < staleCutoff
        //   ErrorPresent : LastErrorMessage IS NOT NULL AND length > 0
        //
        // exceptionOnly=true → row must match at least ONE classification.
        // classification=<X> → row must match that specific classification
        //                      (implies exceptionOnly).
        var requireExceptions = exceptionOnly || classFilter != null;

        if (classFilter == ClassFailed)
        {
            q = q.Where(w => w.Status == "Failed");
        }
        else if (classFilter == ClassCancelled)
        {
            q = q.Where(w => w.Status == "Cancelled");
        }
        else if (classFilter == ClassStuck)
        {
            q = q.Where(w =>
                (w.Status == "Active" || w.Status == "Pending")
                && (w.UpdatedAt ?? w.CreatedAt) < staleCutoff);
        }
        else if (classFilter == ClassErrorPresent)
        {
            q = q.Where(w => w.LastErrorMessage != null && w.LastErrorMessage != "");
        }
        else if (requireExceptions)
        {
            // exceptionOnly with no specific classification → union of all rules.
            q = q.Where(w =>
                w.Status == "Failed"
                || w.Status == "Cancelled"
                || ((w.Status == "Active" || w.Status == "Pending")
                    && (w.UpdatedAt ?? w.CreatedAt) < staleCutoff)
                || (w.LastErrorMessage != null && w.LastErrorMessage != ""));
        }

        var total = await q.CountAsync(ct);

        // Project before paging so we can join the workflow definition's
        // display name and the optional product mapping (source entity)
        // without dragging a fat entity graph into memory.
        //
        // SECURITY: the joined sources also call IgnoreQueryFilters() so
        // PlatformAdmin can read across tenants, but each subquery
        // re-applies an explicit `TenantId == w.TenantId` predicate. This
        // prevents a stray cross-tenant mapping/definition row (data-quality
        // edge case or future bug) from leaking another tenant's
        // source-entity metadata onto an instance row.
        //
        // DETERMINISM: a workflow instance can in theory have more than one
        // ProductWorkflowMapping (e.g. legacy + active). Active rows are
        // preferred; ties are broken by most-recent UpdatedAt then CreatedAt
        // so the selected mapping is stable across query plans.
        var defs = _db.FlowDefinitions.AsNoTracking().IgnoreQueryFilters();
        var maps = _db.ProductWorkflowMappings.AsNoTracking().IgnoreQueryFilters();

        var rows = await q
            .OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(w => new
            {
                Instance = w,
                DefinitionName = defs
                    .Where(d => d.Id == w.WorkflowDefinitionId && d.TenantId == w.TenantId)
                    .Select(d => d.Name)
                    .FirstOrDefault(),
                Mapping = maps
                    .Where(m => m.WorkflowInstanceId == w.Id && m.TenantId == w.TenantId)
                    .OrderByDescending(m => m.Status == "Active" ? 1 : 0)
                    .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                    .Select(m => new { m.SourceEntityType, m.SourceEntityId })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminWorkflowInstanceListItem
        {
            Id                   = r.Instance.Id,
            TenantId             = r.Instance.TenantId,
            ProductKey           = r.Instance.ProductKey,
            WorkflowDefinitionId = r.Instance.WorkflowDefinitionId,
            WorkflowName         = r.DefinitionName,
            Status               = r.Instance.Status,
            CurrentStepKey       = r.Instance.CurrentStepKey,
            AssignedToUserId     = r.Instance.AssignedToUserId,
            CorrelationKey       = r.Instance.CorrelationKey,
            SourceEntityType     = r.Mapping?.SourceEntityType,
            SourceEntityId       = r.Mapping?.SourceEntityId,
            StartedAt            = r.Instance.StartedAt,
            CompletedAt          = r.Instance.CompletedAt,
            UpdatedAt            = r.Instance.UpdatedAt,
            CreatedAt            = r.Instance.CreatedAt,
            LastErrorMessage     = r.Instance.LastErrorMessage,
            // E9.3 — same rules as the server-side filter, evaluated in
            // memory after projection so callers always get the per-row
            // classifications regardless of whether they passed
            // exceptionOnly. Multiple labels can apply.
            Classifications      = ClassifyRow(
                r.Instance.Status,
                r.Instance.UpdatedAt ?? r.Instance.CreatedAt,
                r.Instance.LastErrorMessage,
                staleCutoff),
        }).ToList();

        _logger.LogInformation(
            "AdminWorkflowInstances.List platformAdmin={IsPlatformAdmin} count={Count} total={Total} filters: product={ProductKey} status={Status} tenant={TenantId} search={SearchPresent} exceptionOnly={ExceptionOnly} classification={Classification} staleHours={StaleHours}",
            isPlatformAdmin, items.Count, total, productKey, status, tenantId, !string.IsNullOrWhiteSpace(search),
            exceptionOnly, classFilter, staleHours);

        return Ok(new AdminWorkflowInstanceListResponse
        {
            Items              = items,
            TotalCount         = total,
            Page               = p,
            PageSize           = ps,
            StaleThresholdHours = staleHours,
        });
    }

    /// <summary>
    /// E9.3 — pure classifier shared between the server-side filter
    /// (LINQ-to-SQL above) and per-row tagging (in-memory here). Returns
    /// every label that applies; an empty list means "healthy".
    /// </summary>
    private static List<string> ClassifyRow(
        string status,
        DateTime lastTouchedUtc,
        string? lastErrorMessage,
        DateTime staleCutoffUtc)
    {
        var labels = new List<string>(capacity: 2);
        if (status == "Failed")    labels.Add(ClassFailed);
        if (status == "Cancelled") labels.Add(ClassCancelled);
        if ((status == "Active" || status == "Pending") && lastTouchedUtc < staleCutoffUtc)
            labels.Add(ClassStuck);
        if (!string.IsNullOrEmpty(lastErrorMessage)) labels.Add(ClassErrorPresent);
        return labels;
    }

    /// <summary>
    /// E9.2 — read-only single-instance detail for the Control Center
    /// workflow detail drawer. Mirrors the same admin scoping rules as
    /// <see cref="List"/>: PlatformAdmin can read across tenants;
    /// TenantAdmin can read only rows in their own tenant. Returns 404
    /// (rather than 403) when a TenantAdmin requests a row that exists
    /// in another tenant — the row is intentionally invisible to them.
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

        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (instance is null) return NotFound();

        // For TenantAdmin, hide rows belonging to other tenants.
        if (!isPlatformAdmin && instance.TenantId != scopeTenantId)
        {
            return NotFound();
        }

        // Definition + mapping joins re-apply explicit `TenantId == w.TenantId`
        // for the same defence-in-depth reason as the list endpoint.
        var definitionName = await _db.FlowDefinitions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Id == instance.WorkflowDefinitionId && d.TenantId == instance.TenantId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(ct);

        var mapping = await _db.ProductWorkflowMappings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.WorkflowInstanceId == instance.Id && m.TenantId == instance.TenantId)
            .OrderByDescending(m => m.Status == "Active" ? 1 : 0)
            .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .Select(m => new { m.SourceEntityType, m.SourceEntityId, m.CorrelationKey })
            .FirstOrDefaultAsync(ct);

        // Resolve the current step's display name when possible.
        string? currentStepName = null;
        if (instance.CurrentStageId.HasValue)
        {
            currentStepName = await _db.WorkflowStages
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.Id == instance.CurrentStageId.Value
                         && s.WorkflowDefinitionId == instance.WorkflowDefinitionId
                         && s.TenantId == instance.TenantId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
        }

        var dto = new AdminWorkflowInstanceDetail
        {
            Id                   = instance.Id,
            TenantId             = instance.TenantId,
            ProductKey           = instance.ProductKey,
            WorkflowDefinitionId = instance.WorkflowDefinitionId,
            WorkflowName         = definitionName,
            Status               = instance.Status,
            CurrentStageId       = instance.CurrentStageId,
            CurrentStepKey       = instance.CurrentStepKey,
            CurrentStepName      = currentStepName,
            AssignedToUserId     = instance.AssignedToUserId,
            CorrelationKey       = instance.CorrelationKey ?? mapping?.CorrelationKey,
            SourceEntityType     = mapping?.SourceEntityType,
            SourceEntityId       = mapping?.SourceEntityId,
            StartedAt            = instance.StartedAt,
            CompletedAt          = instance.CompletedAt,
            UpdatedAt            = instance.UpdatedAt,
            CreatedAt            = instance.CreatedAt,
            LastErrorMessage     = instance.LastErrorMessage,
        };

        _logger.LogInformation(
            "AdminWorkflowInstances.GetById id={InstanceId} platformAdmin={IsPlatformAdmin} tenant={TenantId}",
            id, isPlatformAdmin, instance.TenantId);

        return Ok(dto);
    }

    /// <summary>
    /// E13.1 — read-only audit timeline for a single workflow instance.
    /// Returns a normalized, deterministically-ordered (ascending by
    /// occurredAt, tie-broken by event id) list of audit events the
    /// audit service has recorded against
    /// <c>EntityType=WorkflowInstance</c> with the given id.
    ///
    /// <para>
    /// Authorization mirrors <see cref="GetById"/>: PlatformAdmin can
    /// read across tenants; TenantAdmin only within their own tenant;
    /// out-of-scope rows return 404 (no existence leakage). Once the
    /// workflow row's visibility is confirmed, the audit query adapter
    /// is invoked with the workflow's TenantId.
    /// </para>
    ///
    /// <para>
    /// This endpoint is strictly read-only: no DB writes, no outbox
    /// enqueues, no audit emits. When the audit service is not
    /// configured (Audit:BaseUrl empty) the registered adapter is the
    /// empty baseline, and the response is 200 OK with an empty list.
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken ct)
    {
        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        string? scopeTenantId = null;
        if (!isPlatformAdmin)
        {
            try { scopeTenantId = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(scopeTenantId)) return Forbid();
        }

        // Confirm the workflow exists and is visible to this caller
        // BEFORE making the audit call, so an out-of-scope id never
        // causes any cross-tenant probe upstream.
        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(w => new { w.Id, w.TenantId })
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (instance is null) return NotFound();
        if (!isPlatformAdmin && instance.TenantId != scopeTenantId) return NotFound();

        var fetch = await _auditQuery.GetEventsForEntityAsync(
            entityType: "WorkflowInstance",
            entityId:   id.ToString(),
            tenantId:   instance.TenantId,
            cancellationToken: ct);

        var events = AuditTimelineNormalizer.Normalize(fetch.Events);

        _logger.LogInformation(
            "AdminWorkflowInstances.Timeline id={InstanceId} platformAdmin={IsPlatformAdmin} tenant={TenantId} count={Count} truncated={Truncated}",
            id, isPlatformAdmin, instance.TenantId, events.Count, fetch.Truncated);

        return Ok(new AdminWorkflowInstanceTimelineResponse
        {
            WorkflowInstanceId = id,
            TenantId           = instance.TenantId,
            TotalCount         = events.Count,
            Truncated          = fetch.Truncated,
            Events             = events,
        });
    }

    // ── E10.1 — admin actions (Retry / Force Complete / Cancel) ──────────
    //
    // Shared rules across all three:
    //   * Reason is required (non-empty after trim, capped at
    //     MaxReasonLength). Sent verbatim to audit.
    //   * Tenant scoping is identical to GetById: PlatformAdmin can act
    //     across tenants; TenantAdmin can act only on rows in their own
    //     tenant. Out-of-scope rows return 404 (existence not leaked).
    //   * State validation runs in code AFTER the row is loaded so a
    //     stale UI request fails with 409 ProblemDetails rather than
    //     silently corrupting state.
    //   * Audit is written via IAuditAdapter AFTER SaveChangesAsync. The
    //     adapter is documented fire-and-forget safe; we additionally
    //     try/catch + log so an audit-pipeline outage cannot fail the
    //     admin action itself (the structured response + ASP.NET log
    //     line still record the action server-side).
    //   * Concurrency: DbUpdateConcurrencyException → 409.

    /// <summary>
    /// E10.1 — re-enter engine-managed execution after a Failed (or
    /// errored Active/Pending) workflow. Clears LastErrorMessage and
    /// flips Status back to "Active"; the engine will pick up from the
    /// existing CurrentStageId / CurrentStepKey on its next tick. Does
    /// not rewind history.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    public Task<IActionResult> Retry(Guid id, [FromBody] AdminWorkflowActionRequest body, CancellationToken ct)
        => HandleAdminActionAsync(id, ActionRetry, body, ct, (w, _) =>
        {
            // Allowed: Failed, OR (Active|Pending with a captured error)
            var hasErr = !string.IsNullOrEmpty(w.LastErrorMessage);
            var ok = w.Status == "Failed"
                  || ((w.Status == "Active" || w.Status == "Pending") && hasErr);
            if (!ok) return ("not_allowed_in_state", $"Retry is only allowed on Failed workflows or Active/Pending workflows with an error. Current status: {w.Status}.");
            // Mutation: re-arm execution.
            w.Status           = "Active";
            w.LastErrorMessage = null;
            w.UpdatedAt        = DateTime.UtcNow;
            return (null, null);
        });

    /// <summary>
    /// E10.1 — admin override that marks an Active or Pending workflow
    /// as Completed without further engine progression. Captures the
    /// reason in audit so an investigator can see this was a forced
    /// completion rather than an organic terminal transition.
    /// </summary>
    [HttpPost("{id:guid}/force-complete")]
    public Task<IActionResult> ForceComplete(Guid id, [FromBody] AdminWorkflowActionRequest body, CancellationToken ct)
        => HandleAdminActionAsync(id, ActionForceComplete, body, ct, (w, now) =>
        {
            if (w.Status != "Active" && w.Status != "Pending")
                return ("not_allowed_in_state", $"Force complete is only allowed on Active or Pending workflows. Current status: {w.Status}.");
            w.Status      = "Completed";
            w.CompletedAt = now;
            w.UpdatedAt   = now;
            return (null, null);
        });

    /// <summary>
    /// E10.1 — admin override that marks an Active or Pending workflow
    /// as Cancelled. Engine will not advance a Cancelled instance.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] AdminWorkflowActionRequest body, CancellationToken ct)
        => HandleAdminActionAsync(id, ActionCancel, body, ct, (w, now) =>
        {
            if (w.Status != "Active" && w.Status != "Pending")
                return ("not_allowed_in_state", $"Cancel is only allowed on Active or Pending workflows. Current status: {w.Status}.");
            w.Status      = "Cancelled";
            w.CompletedAt = now;
            w.UpdatedAt   = now;
            return (null, null);
        });

    /// <summary>
    /// E10.1 — shared admin-action pipeline. Centralises tenant
    /// scoping, reason validation, state-transition delegation, save,
    /// audit emit, and structured response shaping so each action
    /// endpoint stays a thin descriptor.
    /// </summary>
    private async Task<IActionResult> HandleAdminActionAsync(
        Guid id,
        string action,
        AdminWorkflowActionRequest? body,
        CancellationToken ct,
        Func<WorkflowInstance, DateTime, (string? Code, string? Message)> apply)
    {
        // Reason normalisation FIRST so a malformed request fails fast
        // before we touch the database.
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

        // Load the row tracked so EF can persist the in-place mutation.
        var instance = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (instance is null) return NotFound();

        // For TenantAdmin, hide rows belonging to other tenants. 404 not
        // 403: existence in another tenant is intentionally invisible.
        if (!isPlatformAdmin && instance.TenantId != scopeTenantId)
        {
            return NotFound();
        }

        var previousStatus = instance.Status;
        var now            = DateTime.UtcNow;

        var (errCode, errMsg) = apply(instance, now);
        if (errCode is not null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title:      errCode,
                detail:     errMsg);
        }

        var performedBy = ResolvePerformedBy();

        // LS-FLOW-E10.2 — enqueue the outbox row IN THE SAME EF unit of
        // work as the state mutation. Both writes commit (or roll back)
        // together, so we can no longer end up in the previous "state
        // changed but audit lost" failure mode. The async OutboxProcessor
        // picks the row up shortly after commit and dispatches it to
        // IAuditAdapter (durable audit), and — for retry — additionally
        // emits a structured re-drive log line that operators can monitor
        // as the durable async signal that the action was processed.
        var outboxEventType = action switch
        {
            ActionRetry         => OutboxEventTypes.AdminRetry,
            ActionForceComplete => OutboxEventTypes.AdminForceComplete,
            ActionCancel        => OutboxEventTypes.AdminCancel,
            _                   => $"workflow.admin.{action}",
        };
        _outbox.Enqueue(outboxEventType, instance.Id, new AdminActionPayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            Action:             action,
            PreviousStatus:     previousStatus,
            NewStatus:          instance.Status,
            Reason:             reason,
            PerformedBy:        performedBy,
            IsPlatformAdmin:    isPlatformAdmin,
            OccurredAtUtc:      now));

        // LS-FLOW-E11.2 — only admin Retry can re-enter an actionable
        // step (Failed → Active or error-bearing Active/Pending →
        // Active). The factory dedups against any Open / InProgress
        // task already at (instance, CurrentStepKey), so this call is a
        // no-op when an active task survives the failure — retry alone
        // never duplicates work. force-complete and cancel both flip
        // Status off Active and the factory short-circuits on
        // ineligible status, so they are also safe no-ops here even
        // though we only invoke for retry.
        if (action == ActionRetry)
        {
            await _taskFactory.EnsureForCurrentStepAsync(instance, ct);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title:      "concurrent_state_change",
                detail:     "Another writer modified this workflow while the admin action was being applied. Please reload and try again.");
        }

        _logger.LogInformation(
            "AdminWorkflowInstances.{Action} id={InstanceId} platformAdmin={IsPlatformAdmin} tenant={TenantId} {Previous}->{New} performedBy={PerformedBy}",
            action, instance.Id, isPlatformAdmin, instance.TenantId, previousStatus, instance.Status, performedBy);

        return Ok(new AdminWorkflowActionResult
        {
            WorkflowInstanceId = instance.Id,
            Action             = action,
            PreviousStatus     = previousStatus,
            NewStatus          = instance.Status,
            PerformedBy        = performedBy,
            Timestamp          = now,
            Reason             = reason,
        });
    }

    /// <summary>
    /// E10.1 — best-effort actor identifier for audit. Tries the common
    /// JWT subject claims in priority order; falls back to a sentinel
    /// so an audit row is never produced with an empty performedBy.
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

public sealed record AdminWorkflowInstanceDetail
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string ProductKey { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public string? WorkflowName { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? CurrentStageId { get; init; }
    public string? CurrentStepKey { get; init; }
    public string? CurrentStepName { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? CorrelationKey { get; init; }
    public string? SourceEntityType { get; init; }
    public string? SourceEntityId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? LastErrorMessage { get; init; }
}

public sealed record AdminWorkflowInstanceListItem
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string ProductKey { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public string? WorkflowName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CurrentStepKey { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? CorrelationKey { get; init; }
    public string? SourceEntityType { get; init; }
    public string? SourceEntityId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// E9.3 — last engine error message, surfaced on the row so the
    /// exception view can show a truncated preview without a second
    /// detail call. Always returned (null when none) for E9.1 callers.
    /// </summary>
    public string? LastErrorMessage { get; init; }

    /// <summary>
    /// E9.3 — every classification label that applies to this row. Empty
    /// list means "no current exception". Multiple labels are possible
    /// (e.g. <c>["Failed","ErrorPresent"]</c>).
    /// </summary>
    public List<string> Classifications { get; init; } = new();
}

// ────────────────────────────────────────────────────────────────────────
// E10.1 — admin action contracts. Read-only types belong below; these
// contracts intentionally live alongside the read DTOs because they
// share the same admin auth/tenant model and are versioned together.
// ────────────────────────────────────────────────────────────────────────

/// <summary>
/// E10.1 — request body for every admin action endpoint
/// (<c>retry</c>, <c>force-complete</c>, <c>cancel</c>). The reason is
/// mandatory: it lands verbatim on the audit record so an investigator
/// can trace why an admin overrode the workflow engine.
/// </summary>
public sealed record AdminWorkflowActionRequest
{
    /// <summary>
    /// Operator-provided justification. Must be non-empty after trim.
    /// Capped server-side at <c>MaxReasonLength</c> characters.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// E10.1 — structured response returned by every admin action so the
/// Control Center can refresh the drawer optimistically without an
/// extra GET call. Fields mirror what is also written to audit.
/// </summary>
public sealed record AdminWorkflowActionResult
{
    public Guid     WorkflowInstanceId { get; init; }
    public string   Action             { get; init; } = string.Empty;
    public string   PreviousStatus     { get; init; } = string.Empty;
    public string   NewStatus          { get; init; } = string.Empty;
    public string   PerformedBy        { get; init; } = string.Empty;
    public DateTime Timestamp          { get; init; }
    public string   Reason             { get; init; } = string.Empty;
}

/// <summary>
/// E13.1 — read-only audit timeline response for a single workflow
/// instance. Events are returned in ascending occurred-at order
/// (tie-broken by event id) by <see cref="AuditTimelineNormalizer"/>.
/// </summary>
public sealed record AdminWorkflowInstanceTimelineResponse
{
    public Guid Id => WorkflowInstanceId;
    public Guid WorkflowInstanceId { get; init; }
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Number of events returned in <see cref="Events"/>.</summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// True when the upstream audit query was capped by the adapter's
    /// hard ceiling (1000 events). The Control Center timeline is
    /// expected to be a single-page view; if this ever flips true on
    /// real traffic, the UI should consider linking through to the
    /// audit service for the full record.
    /// </summary>
    public bool Truncated { get; init; }

    public IReadOnlyList<TimelineEvent> Events { get; init; } = Array.Empty<TimelineEvent>();
}

public sealed record AdminWorkflowInstanceListResponse
{
    public List<AdminWorkflowInstanceListItem> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }

    /// <summary>
    /// E9.3 — stale threshold (in hours) used to evaluate the "Stuck"
    /// classification for this response. Echoed back so the UI can label
    /// the column / filter chip ("Stuck >24h") without guessing.
    /// </summary>
    public int StaleThresholdHours { get; init; }
}
