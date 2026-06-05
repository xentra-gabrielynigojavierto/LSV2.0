namespace Flow.Application.DTOs;

/// <summary>
/// LS-FLOW-MERGE-P5 — runtime view of a <see cref="Flow.Domain.Entities.WorkflowInstance"/>.
/// </summary>
public class WorkflowInstanceResponse
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string ProductKey { get; set; } = string.Empty;
    public string? CorrelationKey { get; set; }
    public Guid? InitialTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CurrentStageId { get; set; }
    public string? CurrentStepKey { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// LS-FLOW-MERGE-P5 — view of the instance's current step and the
/// transitions that are currently reachable from it.
/// </summary>
public class WorkflowInstanceCurrentStepResponse
{
    public Guid WorkflowInstanceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CurrentStageId { get; set; }
    public string? CurrentStepKey { get; set; }
    public string? CurrentStepName { get; set; }
    public bool IsTerminal { get; set; }
    public List<WorkflowInstanceTransitionOption> AvailableTransitions { get; set; } = new();
}

public class WorkflowInstanceTransitionOption
{
    public Guid TransitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ToStageId { get; set; }
    public string ToStepKey { get; set; } = string.Empty;
    public string ToStepName { get; set; } = string.Empty;
    public bool IsTerminal { get; set; }
}

public class AdvanceWorkflowRequest
{
    public string ExpectedCurrentStepKey { get; set; } = string.Empty;
    public string? ToStepKey { get; set; }
    public Dictionary<string, string>? Payload { get; set; }
}

public class CancelWorkflowRequest
{
    public string? Reason { get; set; }
}
