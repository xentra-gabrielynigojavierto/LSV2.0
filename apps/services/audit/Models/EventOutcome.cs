namespace PlatformAuditEventService.Models;

public static class EventOutcome
{
    public const string Success = "SUCCESS";
    public const string Failure = "FAILURE";
    public const string Partial = "PARTIAL";
    public const string Unknown = "UNKNOWN";

    private static readonly HashSet<string> Valid =
        new(StringComparer.OrdinalIgnoreCase) { Success, Failure, Partial, Unknown };

    public static bool IsValid(string value) => Valid.Contains(value);
}
