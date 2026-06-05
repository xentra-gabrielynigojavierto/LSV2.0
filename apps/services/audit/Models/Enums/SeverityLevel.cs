namespace PlatformAuditEventService.Enums;

/// <summary>
/// Ordered severity scale for audit events. Numeric values allow range comparisons.
/// Maps to standard syslog / OpenTelemetry severity conventions where applicable.
/// </summary>
public enum SeverityLevel
{
    /// <summary>Verbose diagnostic data — development/trace contexts only.</summary>
    Debug = 1,

    /// <summary>Normal, expected operational activity. Default for successful operations.</summary>
    Info = 2,

    /// <summary>Significant but normal event that operators should be aware of.</summary>
    Notice = 3,

    /// <summary>Recoverable condition that may indicate a problem if sustained.</summary>
    Warn = 4,

    /// <summary>Operation failed or produced unexpected output — requires investigation.</summary>
    Error = 5,

    /// <summary>Severe failure — service degradation likely. Requires immediate attention.</summary>
    Critical = 6,

    /// <summary>System-level failure — data loss, security breach, unrecoverable state.</summary>
    Alert = 7
}
