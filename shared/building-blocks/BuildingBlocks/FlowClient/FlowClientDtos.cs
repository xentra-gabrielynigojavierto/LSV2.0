namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — request to start a product-correlated workflow via Flow.
/// Mirrors <c>Flow.Application.DTOs.CreateProductWorkflowRequest</c> on the
/// wire; the shared client owns its own type to keep BuildingBlocks free of
/// a dependency on Flow.Application.
/// </summary>
public sealed class StartProductWorkflowRequest
{
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CorrelationKey { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToRoleKey { get; set; }
    public string? AssignedToOrgId { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>
/// LS-FLOW-MERGE-P4 — response shape returned by Flow's product-workflow
/// endpoints. Mirrors <c>Flow.Application.DTOs.ProductWorkflowResponse</c>.
/// </summary>
public sealed class FlowProductWorkflowResponse
{
    public Guid Id { get; set; }
    public string ProductKey { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowInstanceTaskId { get; set; }
    public string? CorrelationKey { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// LS-FLOW-MERGE-P5 — response shape for the workflow-instance execution
/// endpoints. Mirrors <c>Flow.Application.DTOs.WorkflowInstanceResponse</c>.
/// </summary>
public sealed class FlowWorkflowInstanceResponse
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
/// LS-FLOW-MERGE-P5 — body for an advance call. Mirrors
/// <c>Flow.Application.DTOs.AdvanceWorkflowRequest</c>.
/// </summary>
public sealed class FlowAdvanceWorkflowRequest
{
    public string ExpectedCurrentStepKey { get; set; } = string.Empty;
    public string? ToStepKey { get; set; }
    public Dictionary<string, string>? Payload { get; set; }
}

/// <summary>
/// E8.1 — slim definition row used by the tenant-portal "Start workflow"
/// modal. Only the fields the UI needs are surfaced; Flow's full
/// <c>WorkflowDefinitionResponse</c> additionally carries stages and
/// transitions which are unused at start time.
/// </summary>
public sealed class FlowWorkflowDefinitionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProductKey { get; set; } = string.Empty;
}

