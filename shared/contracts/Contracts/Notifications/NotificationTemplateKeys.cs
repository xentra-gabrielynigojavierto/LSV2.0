namespace Contracts.Notifications;

/// <summary>
/// Stable, well-known notification template keys (E12.1).
///
/// <para>
/// Naming rules — apply to every key, including future additions:
/// <list type="bullet">
///   <item><description>Lower-case dotted segments: <c>{category}.{event}[.{qualifier}]</c>.</description></item>
///   <item><description>First segment is the originating category — <c>workflow</c>, <c>task</c>, <c>sla</c>, <c>admin</c>.</description></item>
///   <item><description>SLA transitions are nested under their owning category: <c>workflow.sla.dueSoon</c>, not <c>sla.workflow.dueSoon</c>.</description></item>
///   <item><description>Admin actions are nested under the affected category: <c>workflow.admin.retry</c>.</description></item>
///   <item><description>No tenant- or product-specific phrasing in the key itself; per-product overrides are handled at template resolution time.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Producers SHOULD use these constants rather than literal strings so a
/// rename is a compile-time break, not a silent runtime miss.
/// </para>
/// </summary>
public static class NotificationTemplateKeys
{
    // ---------- Workflow lifecycle -----------------------------------
    public const string WorkflowCompleted = "workflow.completed";

    // ---------- Workflow SLA transitions -----------------------------
    public const string WorkflowSlaDueSoon   = "workflow.sla.dueSoon";
    public const string WorkflowSlaOverdue   = "workflow.sla.overdue";
    public const string WorkflowSlaEscalated = "workflow.sla.escalated";

    // ---------- Workflow admin actions -------------------------------
    public const string WorkflowAdminRetry         = "workflow.admin.retry";
    public const string WorkflowAdminForceComplete = "workflow.admin.forceComplete";
    public const string WorkflowAdminCancel        = "workflow.admin.cancel";

    // ---------- Task lifecycle (reserved for future phases) ----------
    public const string TaskAssigned   = "task.assigned";
    public const string TaskReassigned = "task.reassigned";
    public const string TaskCompleted  = "task.completed";
    public const string TaskCancelled  = "task.cancelled";
}
