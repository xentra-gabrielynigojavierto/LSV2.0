using Flow.Domain.Common;
using Flow.Domain.Enums;

namespace Flow.Domain.Entities;

public class WorkflowStage : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TaskItemStatus MappedStatus { get; set; }
    public int Order { get; set; }
    public bool IsInitial { get; set; }
    public bool IsTerminal { get; set; }
    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }

    public FlowDefinition WorkflowDefinition { get; set; } = null!;
    public ICollection<WorkflowTransition> TransitionsFrom { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> TransitionsTo { get; set; } = new List<WorkflowTransition>();
}
