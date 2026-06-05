using BuildingBlocks.Authorization;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.DTOs;
using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-MERGE-P5 — execution surface over <see cref="WorkflowInstance"/>.
///
/// <para>
/// Accepts both end-user bearer tokens (existing
/// <see cref="JwtBearerDefaults.AuthenticationScheme"/>) and
/// machine-to-machine service tokens (HS256, audience <c>flow-service</c>),
/// because the JWT validator is configured with both audiences. Tenant
/// isolation is enforced by the <c>FlowDbContext</c> query filter, so a
/// service token must carry a <c>tid</c> claim that matches the row's
/// tenant or the row will not be visible.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/workflow-instances")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public class WorkflowInstancesController : ControllerBase
{
    private readonly IFlowDbContext _db;
    private readonly IWorkflowEngine _engine;
    private readonly IAuditQueryAdapter _auditQuery;
    private readonly ILogger<WorkflowInstancesController> _logger;

    public WorkflowInstancesController(
        IFlowDbContext db,
        IWorkflowEngine engine,
        IAuditQueryAdapter auditQuery,
        ILogger<WorkflowInstancesController> logger)
    {
        _db = db;
        _engine = engine;
        _auditQuery = auditQuery;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("workflow-instance:{InstanceId}", id);

        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (instance is null) return NotFound();
        return Ok(WorkflowEngine.Map(instance));
    }

    [HttpGet("{id:guid}/current-step")]
    public async Task<IActionResult> GetCurrentStep(Guid id, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("workflow-instance:{InstanceId}", id);

        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, ct);
        if (instance is null) return NotFound();

        var stages = await _db.WorkflowStages
            .AsNoTracking()
            .Where(s => s.WorkflowDefinitionId == instance.WorkflowDefinitionId)
            .ToListAsync(ct);

        var currentStage = stages.FirstOrDefault(s => s.Id == instance.CurrentStageId);

        var transitions = currentStage is null
            ? new List<WorkflowTransition>()
            : await _db.WorkflowTransitions.AsNoTracking()
                .Where(t => t.IsActive
                         && t.WorkflowDefinitionId == instance.WorkflowDefinitionId
                         && t.FromStageId == currentStage.Id)
                .ToListAsync(ct);

        var response = new WorkflowInstanceCurrentStepResponse
        {
            WorkflowInstanceId = instance.Id,
            Status             = instance.Status,
            CurrentStageId     = instance.CurrentStageId,
            CurrentStepKey     = instance.CurrentStepKey,
            CurrentStepName    = currentStage?.Name,
            IsTerminal         = currentStage?.IsTerminal ?? false,
            AvailableTransitions = transitions.Select(t =>
            {
                var to = stages.FirstOrDefault(s => s.Id == t.ToStageId);
                return new WorkflowInstanceTransitionOption
                {
                    TransitionId = t.Id,
                    Name         = t.Name,
                    ToStageId    = t.ToStageId,
                    ToStepKey    = to?.Key ?? string.Empty,
                    ToStepName   = to?.Name ?? string.Empty,
                    IsTerminal   = to?.IsTerminal ?? false
                };
            }).ToList()
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/advance")]
    public async Task<IActionResult> Advance(Guid id, [FromBody] AdvanceWorkflowRequest body, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("workflow-instance:{InstanceId}", id);

        if (body is null || string.IsNullOrWhiteSpace(body.ExpectedCurrentStepKey))
            return BadRequest(new { error = "expectedCurrentStepKey is required." });

        try
        {
            var result = await _engine.AdvanceAsync(id, body.ExpectedCurrentStepKey, body.ToStepKey, ct);
            return Ok(result);
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ValidationException vex) { return BadRequest(new { error = vex.Message, errors = vex.Errors }); }
        catch (InvalidWorkflowTransitionException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("workflow-instance:{InstanceId}", id);

        try
        {
            var result = await _engine.CompleteAsync(id, ct);
            return Ok(result);
        }
        catch (NotFoundException) { return NotFound(); }
        catch (InvalidWorkflowTransitionException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelWorkflowRequest? body, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("workflow-instance:{InstanceId}", id);

        try
        {
            var result = await _engine.CancelAsync(id, body?.Reason, ct);
            return Ok(result);
        }
        catch (NotFoundException) { return NotFound(); }
        catch (InvalidWorkflowTransitionException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    /// <summary>
    /// LS-FLOW-E16 — GET <c>/api/v1/workflow-instances/{id}/timeline</c>.
    /// Tenant-portal variant of the existing E13.1 admin timeline
    /// endpoint. Returns the unified, deterministically-ordered audit
    /// history for a single workflow instance: lifecycle, state
    /// transitions, admin actions, and SLA transitions if recorded.
    ///
    /// <para>
    /// <b>Tenant safety:</b> the workflow lookup runs through the
    /// <c>WorkflowInstance</c> global query filter on
    /// <c>FlowDbContext</c>, so cross-tenant ids surface as 404
    /// (identical to a missing instance — no existence leakage). The
    /// admin counterpart at
    /// <c>/api/v1/admin/workflow-instances/{id}/timeline</c> remains
    /// the only path that can read across tenants.
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType(typeof(WorkflowInstanceTimelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken ct)
    {
        var instance = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new { w.Id, w.TenantId })
            .FirstOrDefaultAsync(ct);

        if (instance is null) return NotFound();

        var result = await WorkflowHistoryQuery.GetForWorkflowInstanceAsync(
            _auditQuery, id, instance.TenantId, ct);

        _logger.LogInformation(
            "WorkflowInstances.Timeline id={InstanceId} tenant={TenantId} count={Count} truncated={Truncated}",
            id, instance.TenantId, result.Events.Count, result.Truncated);

        return Ok(new WorkflowInstanceTimelineResponse
        {
            WorkflowInstanceId = id,
            TotalCount         = result.Events.Count,
            Truncated          = result.Truncated,
            Events             = result.Events,
        });
    }
}

/// <summary>
/// LS-FLOW-E16 — response envelope for the tenant-portal workflow
/// timeline endpoint. Mirrors <c>AdminWorkflowInstanceTimelineResponse</c>
/// so admin and tenant clients consume the same shape.
/// </summary>
public sealed record WorkflowInstanceTimelineResponse
{
    public Guid WorkflowInstanceId { get; init; }
    public int  TotalCount { get; init; }
    public bool Truncated { get; init; }
    public IReadOnlyList<TimelineEvent> Events { get; init; } = Array.Empty<TimelineEvent>();
}
