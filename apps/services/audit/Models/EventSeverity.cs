namespace PlatformAuditEventService.Models;

public static class EventSeverity
{
    public const string Debug    = "DEBUG";
    public const string Info     = "INFO";
    public const string Warn     = "WARN";
    public const string Error    = "ERROR";
    public const string Critical = "CRITICAL";

    private static readonly HashSet<string> Valid =
        new(StringComparer.OrdinalIgnoreCase) { Debug, Info, Warn, Error, Critical };

    public static bool IsValid(string value) => Valid.Contains(value);
}
