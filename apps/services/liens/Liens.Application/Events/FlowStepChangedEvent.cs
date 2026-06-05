namespace Liens.Application.Events;

/// <summary>
/// LS-LIENS-FLOW-009 — payload received at the internal event ingestion endpoint
/// when Flow advances a workflow instance to a new step.
///
/// Naming follows the platform convention for event types (dot-separated lowercase).
/// The <see cref="ProductCode"/> guard ensures only Synq-Liens events are processed.
/// </summary>
public sealed class FlowStepChangedEvent
{
    /// <summary>Must be "workflow.step.changed".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Tenant that owns the workflow instance.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Must be "SYNQ_LIENS" — rejects events from other products.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Flow workflow instance whose step changed.</summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>The step key the instance was on before the transition (informational).</summary>
    public string? PreviousStepKey { get; set; }

    /// <summary>The step key the instance is now on. Used to update task WorkflowStepKey.</summary>
    public string CurrentStepKey { get; set; } = string.Empty;

    /// <summary>ISO 8601 timestamp from Flow — informational, not used for ordering in MVP.</summary>
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Result returned by <see cref="IFlowEventHandler"/> after processing a step-change event.
/// </summary>
public sealed class FlowEventHandleResult
{
    public int Processed { get; init; }
    public int NoOp      { get; init; }
}
