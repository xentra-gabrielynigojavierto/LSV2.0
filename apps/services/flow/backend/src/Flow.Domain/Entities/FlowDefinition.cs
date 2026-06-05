using Flow.Domain.Common;
using Flow.Domain.Enums;

namespace Flow.Domain.Entities;

public class FlowDefinition : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0";
    public FlowStatus Status { get; set; } = FlowStatus.Draft;

    /// <summary>
    /// LS-FLOW-020-A — Product Context Layer. Required, validated against
    /// <see cref="ProductKeys.All"/>. Existing rows backfilled to
    /// <see cref="ProductKeys.FlowGeneric"/>.
    /// </summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    /// <summary>
    /// LS-FLOW-E10.3 — workflow-level default SLA in minutes. When non-null,
    /// new instances of this definition are assigned
    /// <c>WorkflowInstance.DueAt = StartedAt + DefaultSlaMinutes</c>
    /// at <see cref="Application.Engines.WorkflowEngine.WorkflowEngine.StartAsync"/>.
    /// Step-level SLA is intentionally out of scope this phase; see
    /// the E10.3 report Known Issues / Gaps.
    /// </summary>
    public int? DefaultSlaMinutes { get; set; }

    public ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowAutomationHook> AutomationHooks { get; set; } = new List<WorkflowAutomationHook>();
}
