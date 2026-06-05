namespace CareConnect.Domain;

public static class ExceptionType
{
    public const string Unavailable = "Unavailable";
    public const string Holiday     = "Holiday";
    public const string Vacation    = "Vacation";
    public const string Blocked     = "Blocked";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Unavailable,
        Holiday,
        Vacation,
        Blocked
    };

    public static bool IsValid(string value) => All.Contains(value);
}
