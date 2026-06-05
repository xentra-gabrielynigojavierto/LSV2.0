namespace Liens.Domain.Enums;

public static class ServicingStatus
{
    public const string Pending    = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed  = "Completed";
    public const string Escalated  = "Escalated";
    public const string OnHold     = "OnHold";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending, InProgress, Completed, Escalated, OnHold
    };
}
