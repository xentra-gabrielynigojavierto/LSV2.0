using Flow.Domain.Common;

namespace Flow.Domain.Entities;

public class AutomationExecutionLog : BaseEntity
{
    public Guid TaskId { get; set; }
    public Guid WorkflowAutomationHookId { get; set; }

    // Nullable: legacy single-action hooks with no AutomationAction row
    // still produce a single log entry with ActionId = null (fallback path).
    public Guid? ActionId { get; set; }

    // Immutable snapshot of the action that produced this log row, captured
    // at execution time. Remains valid for audit/reporting even if the
    // referenced AutomationAction is later edited or deleted.
    public string ActionType { get; set; } = string.Empty;
    public int ActionOrder { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }

    // LS-FLOW-019-C: number of attempts taken (1 = succeeded/failed on first try;
    // > 1 indicates retries were used). Skipped rows have Attempts = 0.
    public int Attempts { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    public TaskItem Task { get; set; } = null!;
    public WorkflowAutomationHook AutomationHook { get; set; } = null!;
}
