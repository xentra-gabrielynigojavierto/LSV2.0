namespace Contracts.Notifications;

/// <summary>
/// High-level categorisation of a notification for routing, filtering and
/// future user-preference opt-out support. Categories are deliberately
/// coarse — fine-grained per-event opt-out is deferred to a later phase.
/// </summary>
public static class NotificationCategory
{
    /// <summary>Workflow lifecycle events (start, advance, complete, cancel, fail).</summary>
    public const string Workflow = "workflow";

    /// <summary>Workflow / task SLA transitions (due-soon, overdue, escalated).</summary>
    public const string Sla = "sla";

    /// <summary>Task lifecycle events (assigned, reassigned, completed, cancelled).</summary>
    public const string Task = "task";

    /// <summary>Operator / admin actions (force-complete, retry, cancel by admin).</summary>
    public const string Admin = "admin";

    /// <summary>System-level health / configuration notices.</summary>
    public const string System = "system";

    public static readonly IReadOnlyList<string> All = new[] { Workflow, Sla, Task, Admin, System };

    public static bool IsKnown(string category) => All.Contains(category);
}
