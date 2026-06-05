using Flow.Domain.Common;

namespace Flow.Domain.Entities;

public class WorkflowAutomationHook : AuditableEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowTransitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerEventType { get; set; } = string.Empty;

    /// <summary>
    /// LS-FLOW-020-A — Product Context Layer. Always derived from and equal
    /// to the parent <see cref="WorkflowDefinition"/>'s ProductKey.
    /// </summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    // Legacy single-action fields — retained for backward compatibility.
    // Kept in sync with Actions[0] on create/update; used as a fallback in the
    // executor when Actions is empty (e.g. rows predating LS-FLOW-019-A).
    public string ActionType { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }

    public bool IsActive { get; set; } = true;

    public FlowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowTransition WorkflowTransition { get; set; } = null!;

    public ICollection<AutomationAction> Actions { get; set; } = new List<AutomationAction>();
}
