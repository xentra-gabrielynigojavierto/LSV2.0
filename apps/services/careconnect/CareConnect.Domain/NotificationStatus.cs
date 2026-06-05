namespace CareConnect.Domain;

public static class NotificationStatus
{
    public const string Pending   = "Pending";
    public const string Ready     = "Ready";
    public const string Sent      = "Sent";
    public const string Failed    = "Failed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending, Ready, Sent, Failed, Cancelled
    };

    public static bool IsValid(string value) => All.Contains(value);
}
