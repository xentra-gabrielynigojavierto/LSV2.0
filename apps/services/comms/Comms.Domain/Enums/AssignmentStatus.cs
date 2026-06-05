namespace Comms.Domain.Enums;

public static class AssignmentStatus
{
    public const string Unassigned = "Unassigned";
    public const string Queued = "Queued";
    public const string Assigned = "Assigned";
    public const string Accepted = "Accepted";
    public const string Reassigned = "Reassigned";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Unassigned, Queued, Assigned, Accepted, Reassigned
    };
}
