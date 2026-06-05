namespace Notifications.Domain;

/// <summary>
/// Immutable, append-only record of every SMS preference state change.
/// Never updated or deleted — provides full compliance audit trail.
///
/// NewState values: opted_in | opted_out | unknown | help_requested
///
/// Source values:
///   inbound_stop_keyword     — STOP/STOPALL/UNSUBSCRIBE/CANCEL/END/QUIT received via Twilio
///   inbound_start_keyword    — START/YES/UNSTOP received via Twilio
///   inbound_help_keyword     — HELP received via Twilio (preference does not change)
///   manual_update            — operator set via API
///   system_import            — bulk-imported from external list
///   tenant_policy            — set programmatically by policy rule
///   unresolved_inbound_keyword — inbound keyword received but tenant could not be resolved
/// </summary>
public class SmsPreferenceHistory
{
    public Guid Id { get; set; }

    /// <summary>Tenant that owns this preference. Null for unresolved inbound events.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Normalized E.164 phone number of the recipient/contact.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>State before this change. Null if this is the first recorded event.</summary>
    public string? PreviousState { get; set; }

    /// <summary>State after this event: opted_in | opted_out | unknown | help_requested</summary>
    public string NewState { get; set; } = string.Empty;

    /// <summary>Source of this change event.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Human-readable reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Exact keyword text (STOP, START, HELP, etc.).</summary>
    public string? KeywordReceived { get; set; }

    /// <summary>Provider name (twilio, etc.).</summary>
    public string? Provider { get; set; }

    /// <summary>Twilio MessageSid for inbound events.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Resolved provider config ID for inbound events.</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>The inbound Twilio `To` number (our platform/tenant number) — masked.</summary>
    public string? InboundToNumber { get; set; }

    /// <summary>Actor ID for manual updates.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Structured JSON metadata for additional context.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Immutable creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
