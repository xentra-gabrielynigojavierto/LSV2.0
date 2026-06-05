namespace Contracts.Notifications;

/// <summary>
/// Standard severity levels applied to a notification at the contract layer.
/// Used by templates and the inbox UX to decide visual treatment, sort order,
/// and routing rules without each producer inventing its own scale.
/// </summary>
public static class NotificationSeverity
{
    /// <summary>Routine informational message (e.g. workflow completion).</summary>
    public const string Info = "info";

    /// <summary>Action recommended; not yet a breach (e.g. SLA due-soon).</summary>
    public const string Warning = "warning";

    /// <summary>Action required; breach or escalated condition (e.g. SLA overdue / escalated, admin retry).</summary>
    public const string Critical = "critical";

    public static readonly IReadOnlyList<string> All = new[] { Info, Warning, Critical };

    public static bool IsKnown(string severity) => All.Contains(severity);
}
