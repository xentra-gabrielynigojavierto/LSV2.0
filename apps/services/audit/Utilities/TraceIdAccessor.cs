using System.Diagnostics;

namespace PlatformAuditEventService.Utilities;

/// <summary>
/// Provides the current request/trace ID from Activity or HttpContext.
/// </summary>
public static class TraceIdAccessor
{
    public static string? Current() =>
        Activity.Current?.TraceId.ToString()
        ?? Activity.Current?.Id;
}
