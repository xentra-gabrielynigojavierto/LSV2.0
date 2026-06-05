using Flow.Domain.Common;

namespace Flow.Domain.Entities;

public class WorkflowTransition : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid FromStageId { get; set; }
    public Guid ToStageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? RulesJson { get; set; }

    public FlowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowStage FromStage { get; set; } = null!;
    public WorkflowStage ToStage { get; set; } = null!;
}
