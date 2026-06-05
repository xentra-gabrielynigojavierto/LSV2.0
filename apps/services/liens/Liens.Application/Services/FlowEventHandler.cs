using Liens.Application.Events;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-009 / TASK-B04 — processes validated Flow step-change events.
///
/// Post-cutover behaviour: delegates to the canonical Task service via
/// <see cref="ILiensTaskServiceClient.TriggerFlowCallbackAsync"/> so the Task service
/// can batch-update all tasks linked to the workflow instance.
/// Liens no longer owns the task step-key column.
/// </summary>
public sealed class FlowEventHandler : IFlowEventHandler
{
    private const string ExpectedEventType  = "workflow.step.changed";
    private const string ExpectedProductCode = "SYNQ_LIENS";

    private readonly ILiensTaskServiceClient      _taskClient;
    private readonly IAuditPublisher              _audit;
    private readonly ILogger<FlowEventHandler>    _logger;

    public FlowEventHandler(
        ILiensTaskServiceClient      taskClient,
        IAuditPublisher              audit,
        ILogger<FlowEventHandler>    logger)
    {
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    public async Task<FlowEventHandleResult> HandleStepChangedAsync(
        FlowStepChangedEvent evt,
        CancellationToken    ct = default)
    {
        if (!string.Equals(evt.EventType, ExpectedEventType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(evt.ProductCode, ExpectedProductCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "FlowEventHandler: Unexpected event type '{Type}' / product '{Product}' — ignored.",
                evt.EventType, evt.ProductCode);
            return new FlowEventHandleResult { Processed = 0, NoOp = 0 };
        }

        try
        {
            await _taskClient.TriggerFlowCallbackAsync(
                evt.TenantId, evt.WorkflowInstanceId, evt.CurrentStepKey, ct);

            _audit.Publish(
                eventType:   "liens.task.flow_step_synced",
                action:      "update",
                description: $"Flow step '{evt.CurrentStepKey}' synced to Task service for instance {evt.WorkflowInstanceId}",
                tenantId:    evt.TenantId,
                actorUserId: Guid.Empty,
                entityType:  "WorkflowInstance",
                entityId:    evt.WorkflowInstanceId.ToString());

            _logger.LogInformation(
                "FlowEventHandler: WorkflowInstance {InstanceId} step='{Step}' callback sent to Task service.",
                evt.WorkflowInstanceId, evt.CurrentStepKey);

            return new FlowEventHandleResult { Processed = 1, NoOp = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FlowEventHandler: flow-callback call failed for WorkflowInstance {InstanceId} step='{Step}'.",
                evt.WorkflowInstanceId, evt.CurrentStepKey);

            return new FlowEventHandleResult { Processed = 0, NoOp = 1 };
        }
    }
}
