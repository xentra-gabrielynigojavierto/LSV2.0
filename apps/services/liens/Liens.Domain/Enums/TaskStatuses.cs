namespace Liens.Domain.Enums;

public static class TaskStatuses
{
    public const string New             = "NEW";
    public const string InProgress      = "IN_PROGRESS";
    public const string WaitingBlocked  = "WAITING_BLOCKED";
    public const string Completed       = "COMPLETED";
    public const string Cancelled       = "CANCELLED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        New, InProgress, WaitingBlocked, Completed, Cancelled
    };

    public static readonly IReadOnlySet<string> Active = new HashSet<string>
    {
        New, InProgress, WaitingBlocked
    };
}
