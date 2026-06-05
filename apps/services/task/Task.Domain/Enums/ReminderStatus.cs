namespace Task.Domain.Enums;

public static class ReminderStatus
{
    public const string Pending   = "PENDING";
    public const string Sent      = "SENT";
    public const string Failed    = "FAILED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Pending, Sent, Failed, Cancelled,
        };
}
