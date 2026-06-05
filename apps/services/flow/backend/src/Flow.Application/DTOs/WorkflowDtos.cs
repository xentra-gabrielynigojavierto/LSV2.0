using Flow.Domain.Enums;

namespace Flow.Application.DTOs;

public record WorkflowDefinitionResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Version { get; init; } = string.Empty;
    public FlowStatus Status { get; init; }
    // LS-FLOW-020-A — always populated; defaults to FLOW_GENERIC for legacy rows.
    public string ProductKey { get; init; } = string.Empty;
    public List<WorkflowStageResponse> Stages { get; init; } = new();
    public List<WorkflowTransitionResponse> Transitions { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public record WorkflowStageResponse
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TaskItemStatus MappedStatus { get; init; }
    public int Order { get; init; }
    public bool IsInitial { get; init; }
    public bool IsTerminal { get; init; }
    public double? CanvasX { get; init; }
    public double? CanvasY { get; init; }
}

public record WorkflowTransitionResponse
{
    public Guid Id { get; init; }
    public Guid FromStageId { get; init; }
    public Guid ToStageId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? RulesJson { get; init; }
}

public record WorkflowDefinitionSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Version { get; init; } = string.Empty;
    public FlowStatus Status { get; init; }
    public string ProductKey { get; init; } = string.Empty;
    public int StageCount { get; init; }
    public int TransitionCount { get; init; }
}

public record CreateWorkflowRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    /// <summary>LS-FLOW-020-A — Optional during transition; defaults to FLOW_GENERIC if omitted.</summary>
    public string? ProductKey { get; init; }
}

public record UpdateWorkflowRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public FlowStatus? Status { get; init; }
    /// <summary>LS-FLOW-020-A — Optional. If omitted the existing ProductKey is preserved.</summary>
    public string? ProductKey { get; init; }
}

public record CreateStageRequest
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TaskItemStatus MappedStatus { get; init; }
    public int Order { get; init; }
    public bool IsInitial { get; init; }
    public bool IsTerminal { get; init; }
    public double? CanvasX { get; init; }
    public double? CanvasY { get; init; }
}

public record UpdateStageRequest
{
    public string Name { get; init; } = string.Empty;
    public TaskItemStatus MappedStatus { get; init; }
    public int Order { get; init; }
    public bool IsInitial { get; init; }
    public bool IsTerminal { get; init; }
    public double? CanvasX { get; init; }
    public double? CanvasY { get; init; }
}

public record CreateTransitionRequest
{
    public Guid FromStageId { get; init; }
    public Guid ToStageId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? RulesJson { get; init; }
}

public record UpdateTransitionRequest
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string? RulesJson { get; init; }
}
