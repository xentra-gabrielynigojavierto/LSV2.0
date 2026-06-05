namespace PlatformAuditEventService.Enums;

/// <summary>
/// Lifecycle state of an export job.
/// Transitions: Pending → Processing → Completed | Failed.
/// Terminal states: Completed, Failed, Cancelled, Expired.
/// </summary>
public enum ExportStatus
{
    /// <summary>Job created, not yet picked up by the export worker.</summary>
    Pending = 1,

    /// <summary>Export worker is actively building the output file.</summary>
    Processing = 2,

    /// <summary>Output file produced and available at FilePath.</summary>
    Completed = 3,

    /// <summary>Export failed — see ErrorMessage for details.</summary>
    Failed = 4,

    /// <summary>Cancelled before processing began or while processing.</summary>
    Cancelled = 5,

    /// <summary>Completed file has exceeded its retention window and was purged.</summary>
    Expired = 6
}
