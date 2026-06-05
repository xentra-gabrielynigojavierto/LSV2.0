namespace PlatformAuditEventService.Enums;

/// <summary>
/// Lifecycle status of an <see cref="PlatformAuditEventService.Entities.AuditAlert"/>.
///
/// Transitions:
///   Open          → Acknowledged (manual)
///   Open          → Resolved     (manual)
///   Acknowledged  → Resolved     (manual)
///   Resolved      → Open         (new detection outside cooldown creates a new record)
///
/// Stored as tinyint in the database.
/// </summary>
public enum AlertStatus : byte
{
    /// <summary>Alert is active and has not been reviewed.</summary>
    Open = 0,

    /// <summary>Alert has been seen/acknowledged by an operator. Condition may still be active.</summary>
    Acknowledged = 1,

    /// <summary>Alert has been resolved. Condition may or may not have cleared.</summary>
    Resolved = 2,
}
