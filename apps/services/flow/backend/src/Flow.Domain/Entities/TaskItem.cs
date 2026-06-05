using Flow.Domain.Common;
using Flow.Domain.Enums;

namespace Flow.Domain.Entities;

public class TaskItem : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Open;

    /// <summary>
    /// LS-FLOW-020-A — Product Context Layer. Must match the linked
    /// <see cref="WorkflowDefinition"/>'s ProductKey when one is assigned.
    /// </summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    public Guid? FlowDefinitionId { get; set; }
    public Guid? WorkflowStageId { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToRoleKey { get; set; }
    public string? AssignedToOrgId { get; set; }
    public DateTime? DueDate { get; set; }
    public ContextReference? Context { get; set; }

    public FlowDefinition? WorkflowDefinition { get; set; }
    public WorkflowStage? WorkflowStage { get; set; }
}
