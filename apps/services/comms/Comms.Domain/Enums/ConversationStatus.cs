namespace Comms.Domain.Enums;

public static class ConversationStatus
{
    public const string New = "New";
    public const string Open = "Open";
    public const string PendingInternal = "PendingInternal";
    public const string PendingExternal = "PendingExternal";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Archived = "Archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        New, Open, PendingInternal, PendingExternal, Resolved, Closed, Archived
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Closed, Archived
    };

    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        { New, new HashSet<string> { Open } },
        { Open, new HashSet<string> { PendingInternal, PendingExternal, Resolved, Closed } },
        { PendingInternal, new HashSet<string> { Open } },
        { PendingExternal, new HashSet<string> { Open } },
        { Resolved, new HashSet<string> { Closed, Open } },
        { Closed, new HashSet<string> { Open, Archived } },
        { Archived, new HashSet<string>() },
    };

    public static bool IsValidTransition(string from, string to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
