using System.Text.Json;
using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Application.Outbox;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Engines.WorkflowEngine;

/// <summary>
/// LS-FLOW-MERGE-P5 — production implementation of <see cref="IWorkflowEngine"/>.
///
/// <para>
/// Reuses the existing <see cref="WorkflowStage"/> + <see cref="WorkflowTransition"/>
/// graph as the step model — no parallel "step" table introduced. The
/// stage <c>Key</c> is treated as the canonical step key (matches the
/// <c>currentStepKey</c> string callers pass on advance).
/// </para>
///
/// <para>
/// Concurrency: all mutating calls run inside the EF execution strategy
/// with a transaction so a partial advance (e.g. status flipped but
/// stage not updated) cannot be observed.
/// </para>
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    public const string StatusActive    = "Active";
    public const string StatusCompleted = "Completed";
    public const string StatusCancelled = "Cancelled";
    public const string StatusFailed    = "Failed";

    // LS-FLOW-E10.3 — initial SLA status when a definition has no
    // configured DefaultSlaMinutes and no explicit per-instance DueAt.
    private const string SlaInitial = WorkflowSlaStatus.OnTrack;

    private readonly IFlowDbContext _db;
    private readonly IOutboxWriter _outbox;
    private readonly IWorkflowTaskFromWorkflowFactory _taskFactory;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IFlowDbContext db,
        IOutboxWriter outbox,
        IWorkflowTaskFromWorkflowFactory taskFactory,
        ILogger<WorkflowEngine> logger)
    {
        _db = db;
        _outbox = outbox;
        _taskFactory = taskFactory;
        _logger = logger;
    }

    public async Task<WorkflowInstanceResponse> StartAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var (instance, stages, _) = await LoadInstanceAndDefinitionAsync(workflowInstanceId, ct);

        // Idempotent: already started.
        if (instance.CurrentStageId.HasValue)
        {
            return Map(instance);
        }

        var initial = stages.FirstOrDefault(s => s.IsInitial)
                      ?? stages.OrderBy(s => s.Order).FirstOrDefault()
                      ?? throw new InvalidWorkflowTransitionException(
                          $"Workflow definition {instance.WorkflowDefinitionId} has no stages.",
                          "no_initial_stage");

        var fromStatus = instance.Status;
        instance.CurrentStageId   = initial.Id;
        instance.CurrentStepKey   = initial.Key;
        instance.StartedAt      ??= DateTime.UtcNow;
        instance.Status           = initial.IsTerminal ? StatusCompleted : StatusActive;
        if (initial.IsTerminal) instance.CompletedAt ??= DateTime.UtcNow;
        instance.LastErrorMessage = null;

        // LS-FLOW-E10.3 — assign DueAt with the documented precedence:
        //   1. caller-provided DueAt on the instance (left untouched)
        //   2. definition.DefaultSlaMinutes added to StartedAt
        //   3. none (DueAt remains null; evaluator skips this instance)
        // Initial SlaStatus is always OnTrack — the evaluator promotes it
        // on its first tick once now() crosses the dueSoon threshold.
        if (instance.DueAt is null)
        {
            var defaultSla = await _db.FlowDefinitions
                .Where(d => d.Id == instance.WorkflowDefinitionId)
                .Select(d => d.DefaultSlaMinutes)
                .FirstOrDefaultAsync(ct);
            if (defaultSla is int minutes && minutes > 0)
            {
                instance.DueAt = instance.StartedAt!.Value.AddMinutes(minutes);
            }
        }
        instance.SlaStatus              = SlaInitial;
        instance.OverdueSince           = null;
        instance.EscalationLevel        = 0;
        instance.LastSlaEvaluatedAt     = null;

        // LS-FLOW-E10.2 — outbox write committed atomically with the
        // status/step mutation by the single SaveChangesAsync below.
        _outbox.Enqueue(OutboxEventTypes.WorkflowStart, instance.Id, new WorkflowLifecyclePayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            FromStepKey:        null,
            ToStepKey:          instance.CurrentStepKey,
            FromStatus:         fromStatus,
            ToStatus:           instance.Status,
            Reason:             null,
            PerformedBy:        null,
            OccurredAtUtc:      DateTime.UtcNow));

        // LS-FLOW-E11.2 — stage a WorkflowTask for the new step in the
        // SAME unit-of-work. Factory enforces eligibility (Active +
        // CurrentStepKey set) and idempotent dedup; terminal initial
        // stages and re-runs are no-ops. The single SaveChangesAsync
        // below commits the task atomically with the instance + outbox
        // row, so a failed save cannot leave an orphan task behind.
        await _taskFactory.EnsureForCurrentStepAsync(instance, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WorkflowEngine.Start instance={InstanceId} tenant={TenantId} product={ProductKey} step={StepKey}",
            instance.Id, instance.TenantId, instance.ProductKey, instance.CurrentStepKey);

        return Map(instance);
    }

    public async Task<WorkflowInstanceResponse> AdvanceAsync(
        Guid workflowInstanceId,
        string expectedCurrentStepKey,
        string? toStepKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expectedCurrentStepKey))
            throw new ValidationException("expectedCurrentStepKey is required.");

        var (instance, stages, transitions) = await LoadInstanceAndDefinitionAsync(workflowInstanceId, ct);

        if (instance.Status != StatusActive)
        {
            throw new InvalidWorkflowTransitionException(
                $"Workflow instance {workflowInstanceId} is {instance.Status}, cannot advance.",
                "instance_not_active");
        }

        if (instance.CurrentStageId is null)
        {
            throw new InvalidWorkflowTransitionException(
                $"Workflow instance {workflowInstanceId} has not been started.",
                "instance_not_started");
        }

        // Optimistic concurrency: the caller's expected current step must
        // match the persisted one. Mismatch is "someone else moved this".
        if (!string.Equals(instance.CurrentStepKey, expectedCurrentStepKey, StringComparison.Ordinal))
        {
            throw new InvalidWorkflowTransitionException(
                $"Expected current step '{expectedCurrentStepKey}' but instance is at '{instance.CurrentStepKey}'.",
                "stale_current_step");
        }

        var fromStage = stages.First(s => s.Id == instance.CurrentStageId);

        var outbound = transitions
            .Where(t => t.IsActive && t.FromStageId == fromStage.Id)
            .ToList();

        if (outbound.Count == 0)
        {
            throw new InvalidWorkflowTransitionException(
                $"No outbound transitions from step '{fromStage.Key}'.",
                "no_outbound_transition");
        }

        WorkflowTransition transition;
        if (!string.IsNullOrWhiteSpace(toStepKey))
        {
            var toStage = stages.FirstOrDefault(s => s.Key == toStepKey)
                          ?? throw new InvalidWorkflowTransitionException(
                              $"Target step '{toStepKey}' is not a stage of definition {instance.WorkflowDefinitionId}.",
                              "unknown_target_step");

            transition = outbound.FirstOrDefault(t => t.ToStageId == toStage.Id)
                         ?? throw new InvalidWorkflowTransitionException(
                             $"No active transition from '{fromStage.Key}' to '{toStepKey}'.",
                             "no_matching_transition");
        }
        else
        {
            if (outbound.Count > 1)
            {
                throw new InvalidWorkflowTransitionException(
                    $"Step '{fromStage.Key}' has multiple outbound transitions; toStepKey is required.",
                    "ambiguous_transition");
            }
            transition = outbound[0];
        }

        var nextStage = stages.First(s => s.Id == transition.ToStageId);

        var fromStatus = instance.Status;
        instance.CurrentStageId   = nextStage.Id;
        instance.CurrentStepKey   = nextStage.Key;
        instance.LastErrorMessage = null;
        if (nextStage.IsTerminal)
        {
            instance.Status      = StatusCompleted;
            instance.CompletedAt = DateTime.UtcNow;
        }

        // LS-FLOW-E10.2 — outbox write committed atomically with the
        // status/step mutation by the conditional UPDATE below.
        var advanceEventType = nextStage.IsTerminal
            ? OutboxEventTypes.WorkflowComplete
            : OutboxEventTypes.WorkflowAdvance;
        _outbox.Enqueue(advanceEventType, instance.Id, new WorkflowLifecyclePayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            FromStepKey:        fromStage.Key,
            ToStepKey:          nextStage.Key,
            FromStatus:         fromStatus,
            ToStatus:           instance.Status,
            Reason:             null,
            PerformedBy:        null,
            OccurredAtUtc:      DateTime.UtcNow));

        // LS-FLOW-E11.2 — stage a WorkflowTask for the new step. The
        // factory short-circuits when nextStage.IsTerminal (Status flips
        // to Completed above) so terminal advances do not create a task,
        // and dedups against any Open / InProgress task that already
        // exists for (instance, step) — covering the loop-back case
        // where a definition has a transition that lands back on a
        // step the operator is still working.
        await _taskFactory.EnsureForCurrentStepAsync(instance, ct);

        await SaveWithConcurrencyAsync(ct, "stale_current_step",
            $"Concurrent update detected; expected step '{expectedCurrentStepKey}' is no longer current.");

        _logger.LogInformation(
            "WorkflowEngine.Advance instance={InstanceId} tenant={TenantId} product={ProductKey} from={From} to={To} terminal={Terminal}",
            instance.Id, instance.TenantId, instance.ProductKey, fromStage.Key, nextStage.Key, nextStage.IsTerminal);

        return Map(instance);
    }

    public async Task<WorkflowInstanceResponse> CompleteAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var (instance, _, _) = await LoadInstanceAndDefinitionAsync(workflowInstanceId, ct);

        if (instance.Status == StatusCompleted) return Map(instance);
        if (instance.Status != StatusActive)
        {
            throw new InvalidWorkflowTransitionException(
                $"Workflow instance {workflowInstanceId} is {instance.Status}, cannot complete.",
                "instance_not_active");
        }

        var fromStatus = instance.Status;
        instance.Status           = StatusCompleted;
        instance.CompletedAt      = DateTime.UtcNow;
        instance.LastErrorMessage = null;

        _outbox.Enqueue(OutboxEventTypes.WorkflowComplete, instance.Id, new WorkflowLifecyclePayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            FromStepKey:        instance.CurrentStepKey,
            ToStepKey:          instance.CurrentStepKey,
            FromStatus:         fromStatus,
            ToStatus:           instance.Status,
            Reason:             null,
            PerformedBy:        null,
            OccurredAtUtc:      DateTime.UtcNow));

        await SaveWithConcurrencyAsync(ct, "concurrent_state_change",
            $"Concurrent update detected on workflow instance {workflowInstanceId}.");

        _logger.LogInformation(
            "WorkflowEngine.Complete instance={InstanceId} tenant={TenantId} product={ProductKey} step={StepKey}",
            instance.Id, instance.TenantId, instance.ProductKey, instance.CurrentStepKey);

        return Map(instance);
    }

    public async Task<WorkflowInstanceResponse> CancelAsync(Guid workflowInstanceId, string? reason, CancellationToken ct = default)
    {
        var (instance, _, _) = await LoadInstanceAndDefinitionAsync(workflowInstanceId, ct);

        if (instance.Status == StatusCancelled) return Map(instance);
        if (instance.Status != StatusActive)
        {
            throw new InvalidWorkflowTransitionException(
                $"Workflow instance {workflowInstanceId} is {instance.Status}, cannot cancel.",
                "instance_not_active");
        }

        var fromStatus = instance.Status;
        instance.Status           = StatusCancelled;
        instance.CompletedAt      = DateTime.UtcNow;
        instance.LastErrorMessage = string.IsNullOrWhiteSpace(reason) ? null : Truncate(reason!, 2048);

        _outbox.Enqueue(OutboxEventTypes.WorkflowCancel, instance.Id, new WorkflowLifecyclePayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            FromStepKey:        instance.CurrentStepKey,
            ToStepKey:          instance.CurrentStepKey,
            FromStatus:         fromStatus,
            ToStatus:           instance.Status,
            Reason:             reason,
            PerformedBy:        null,
            OccurredAtUtc:      DateTime.UtcNow));

        await SaveWithConcurrencyAsync(ct, "concurrent_state_change",
            $"Concurrent update detected on workflow instance {workflowInstanceId}.");

        _logger.LogInformation(
            "WorkflowEngine.Cancel instance={InstanceId} tenant={TenantId} product={ProductKey} step={StepKey} reason={Reason}",
            instance.Id, instance.TenantId, instance.ProductKey, instance.CurrentStepKey, reason);

        return Map(instance);
    }

    public async Task<WorkflowInstanceResponse> FailAsync(Guid workflowInstanceId, string errorMessage, CancellationToken ct = default)
    {
        var (instance, _, _) = await LoadInstanceAndDefinitionAsync(workflowInstanceId, ct);

        if (instance.Status != StatusActive)
        {
            throw new InvalidWorkflowTransitionException(
                $"Workflow instance {workflowInstanceId} is {instance.Status}, cannot mark failed.",
                "instance_not_active");
        }

        var fromStatus = instance.Status;
        instance.Status           = StatusFailed;
        instance.CompletedAt      = DateTime.UtcNow;
        instance.LastErrorMessage = Truncate(errorMessage ?? "Failed", 2048);

        _outbox.Enqueue(OutboxEventTypes.WorkflowFail, instance.Id, new WorkflowLifecyclePayload(
            WorkflowInstanceId: instance.Id,
            ProductKey:         instance.ProductKey,
            FromStepKey:        instance.CurrentStepKey,
            ToStepKey:          instance.CurrentStepKey,
            FromStatus:         fromStatus,
            ToStatus:           instance.Status,
            Reason:             instance.LastErrorMessage,
            PerformedBy:        null,
            OccurredAtUtc:      DateTime.UtcNow));

        await SaveWithConcurrencyAsync(ct, "concurrent_state_change",
            $"Concurrent update detected on workflow instance {workflowInstanceId}.");

        _logger.LogWarning(
            "WorkflowEngine.Fail instance={InstanceId} tenant={TenantId} product={ProductKey} step={StepKey} error={Error}",
            instance.Id, instance.TenantId, instance.ProductKey, instance.CurrentStepKey, instance.LastErrorMessage);

        return Map(instance);
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private async Task<(WorkflowInstance instance, List<WorkflowStage> stages, List<WorkflowTransition> transitions)>
        LoadInstanceAndDefinitionAsync(Guid id, CancellationToken ct)
    {
        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("WorkflowInstance", id);

        var stages = await _db.WorkflowStages
            .Where(s => s.WorkflowDefinitionId == instance.WorkflowDefinitionId)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);

        var transitions = await _db.WorkflowTransitions
            .Where(t => t.WorkflowDefinitionId == instance.WorkflowDefinitionId)
            .ToListAsync(ct);

        return (instance, stages, transitions);
    }

    public static WorkflowInstanceResponse Map(WorkflowInstance i) => new()
    {
        Id                   = i.Id,
        WorkflowDefinitionId = i.WorkflowDefinitionId,
        ProductKey           = i.ProductKey,
        CorrelationKey       = i.CorrelationKey,
        InitialTaskId        = i.InitialTaskId,
        Status               = i.Status,
        CurrentStageId       = i.CurrentStageId,
        CurrentStepKey       = i.CurrentStepKey,
        StartedAt            = i.StartedAt,
        CompletedAt          = i.CompletedAt,
        AssignedToUserId     = i.AssignedToUserId,
        LastErrorMessage     = i.LastErrorMessage,
        CreatedAt            = i.CreatedAt,
        UpdatedAt            = i.UpdatedAt
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    /// <summary>
    /// LS-FLOW-MERGE-P5 — wraps <c>SaveChangesAsync</c> and translates the
    /// EF concurrency-token failure (raised when another writer changed
    /// <c>CurrentStepKey</c> or <c>Status</c> after our load) into an
    /// <see cref="InvalidWorkflowTransitionException"/> with the supplied
    /// machine-readable code, which the controller maps to HTTP 409.
    /// </summary>
    private async Task SaveWithConcurrencyAsync(CancellationToken ct, string code, string message)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidWorkflowTransitionException(message, code);
        }
    }
}
