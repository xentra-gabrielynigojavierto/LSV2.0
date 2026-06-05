namespace Notifications.Domain;

/// <summary>
/// Tracks a recipient's SMS opt-in/opt-out preference state for a specific tenant.
/// Preference states: opted_in | opted_out | unknown
///
/// Sources:
///   inbound_stop_keyword  — STOP/STOPALL/UNSUBSCRIBE/CANCEL/END/QUIT received via Twilio inbound
///   inbound_start_keyword — START/YES/UNSTOP received via Twilio inbound
///   manual_update         — operator set via API
///   system_import         — bulk-imported from external list
///   tenant_policy         — set programmatically by tenant policy rules
/// </summary>
public class SmsContactPreference
{
    public Guid Id { get; set; }

    /// <summary>Tenant this preference belongs to. May be null if tenant could not be resolved from inbound webhook.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Normalized E.164 phone number (digits and leading + only, no spaces/dashes).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Current preference state: opted_in | opted_out | unknown</summary>
    public string PreferenceState { get; set; } = "unknown";

    /// <summary>Source of the last state change.</summary>
    public string? Source { get; set; }

    /// <summary>Human-readable reason for the current state.</summary>
    public string? Reason { get; set; }

    /// <summary>Exact keyword text that triggered this state change (e.g. STOP, START). Null for non-keyword sources.</summary>
    public string? KeywordReceived { get; set; }

    /// <summary>Provider message ID (e.g. Twilio MessageSid) of the inbound message that triggered this change.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Actor who set the preference for manual updates (JWT subject / operator ID).</summary>
    public string? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
