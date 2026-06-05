namespace Task.Domain.Enums;

public static class TaskStatus
{
    public const string Open           = "OPEN";
    public const string InProgress     = "IN_PROGRESS";
    public const string WaitingBlocked = "WAITING_BLOCKED";
    public const string Completed      = "COMPLETED";
    public const string Cancelled      = "CANCELLED";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Open, InProgress, WaitingBlocked, Completed, Cancelled,
        };

    public static readonly IReadOnlySet<string> Terminal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Completed, Cancelled,
        };

    public static bool IsTerminal(string status) => Terminal.Contains(status);
}
