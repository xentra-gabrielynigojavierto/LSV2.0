namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E11.1 — string-constant priority values for
/// <see cref="Domain.Entities.WorkflowTask"/>. Stored as a short
/// <c>varchar</c> for the same reasons as <see cref="WorkflowTaskStatus"/>:
/// stable wire format, no ordinal coupling, easy to extend.
/// Default is <see cref="Normal"/>.
/// </summary>
public static class WorkflowTaskPriority
{
    public const string Low    = "Low";
    public const string Normal = "Normal";
    public const string High   = "High";
    public const string Urgent = "Urgent";

    public static bool IsKnown(string? priority) =>
        priority is Low or Normal or High or Urgent;
}
