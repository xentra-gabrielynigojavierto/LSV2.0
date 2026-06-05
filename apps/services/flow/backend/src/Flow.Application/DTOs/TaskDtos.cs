using Flow.Domain.Enums;

namespace Flow.Application.DTOs;

public record CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? FlowDefinitionId { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? AssignedToRoleKey { get; init; }
    public string? AssignedToOrgId { get; init; }
    public DateTime? DueDate { get; init; }
    public ContextReferenceDto? Context { get; init; }
    /// <summary>
    /// LS-FLOW-020-A — Resolution precedence:
    /// 1) explicit value here, 2) inferred from linked workflow, 3) FLOW_GENERIC.
    /// If both are supplied and the workflow's ProductKey differs, the request is rejected.
    /// </summary>
    public string? ProductKey { get; init; }
}

public record UpdateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? FlowDefinitionId { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? AssignedToRoleKey { get; init; }
    public string? AssignedToOrgId { get; init; }
    public DateTime? DueDate { get; init; }
    public ContextReferenceDto? Context { get; init; }
    /// <summary>LS-FLOW-020-A — Optional. If omitted the task's existing ProductKey is preserved.</summary>
    public string? ProductKey { get; init; }
}

public record UpdateTaskStatusRequest
{
    public TaskItemStatus Status { get; init; }
}

public record AssignTaskRequest
{
    public string? AssignedToUserId { get; init; }
    public string? AssignedToRoleKey { get; init; }
    public string? AssignedToOrgId { get; init; }
}

public record ContextReferenceDto
{
    public string ContextType { get; init; } = string.Empty;
    public string ContextId { get; init; } = string.Empty;
    public string? Label { get; init; }
}

public record TaskResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TaskItemStatus Status { get; init; }
    public string ProductKey { get; init; } = string.Empty;
    public Guid? FlowDefinitionId { get; init; }
    public Guid? WorkflowStageId { get; init; }
    public string? WorkflowName { get; init; }
    public string? WorkflowStageName { get; init; }
    public List<TaskItemStatus>? AllowedNextStatuses { get; init; }
    public Dictionary<string, TransitionRuleHints>? AllowedTransitionRules { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? AssignedToRoleKey { get; init; }
    public string? AssignedToOrgId { get; init; }
    public DateTime? DueDate { get; init; }
    public ContextReferenceDto? Context { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public record TransitionRuleHints
{
    public bool RequireTitle { get; init; }
    public bool RequireDescription { get; init; }
    public bool RequireAssignment { get; init; }
    public bool RequireDueDate { get; init; }
}

public record TaskListQuery
{
    public TaskItemStatus? Status { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? AssignedToRoleKey { get; init; }
    public string? AssignedToOrgId { get; init; }
    public string? ContextType { get; init; }
    public string? ContextId { get; init; }
    /// <summary>LS-FLOW-020-A — When omitted, all products within the tenant are returned (transitional default).</summary>
    public string? ProductKey { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";
}

public record PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
