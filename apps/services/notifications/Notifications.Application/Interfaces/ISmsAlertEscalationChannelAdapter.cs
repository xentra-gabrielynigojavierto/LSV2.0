using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-011: Result of a single escalation delivery attempt.
/// </summary>
public sealed class EscalationDeliveryResult
{
    public bool Success { get; init; }

    /// <summary>Whether the failure is transient and eligible for retry.</summary>
    public bool Retryable { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Provider-assigned message ID if the channel returned one.
    /// Safe to store — must not contain credentials or phone numbers.
    /// </summary>
    public string? ExternalMessageId { get; init; }

    public static EscalationDeliveryResult Succeeded(string? externalMessageId = null) =>
        new() { Success = true, ExternalMessageId = externalMessageId };

    public static EscalationDeliveryResult Failed(string errorCode, string errorMessage, bool retryable) =>
        new() { Success = false, Retryable = retryable, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// LS-NOTIF-SMS-011: Safe escalation payload built from an SmsOperationalAlert.
/// Contains only safe, non-sensitive alert metadata.
/// No credentials, phone numbers, CredentialsJson, or SettingsJson.
/// </summary>
public sealed class EscalationPayload
{
    /// <summary>Plain-text notification subject/title.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Plain-text notification body.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Structured Teams MessageCard JSON string (null if not built yet).</summary>
    public string? TeamsPayloadJson { get; init; }

    /// <summary>Structured Slack blocks JSON string (null if not built yet).</summary>
    public string? SlackPayloadJson { get; init; }

    /// <summary>
    /// SHA-256 hex of a canonical string derived from safe alert fields.
    /// Used for deduplication within the cooldown window.
    /// </summary>
    public string PayloadHash { get; init; } = string.Empty;
}

/// <summary>
/// LS-NOTIF-SMS-011: Channel-specific delivery adapter for SMS alert escalations.
///
/// Constraints:
///   - Must NEVER trigger SMS sends.
///   - Must NEVER call SMS providers.
///   - Must NEVER log raw webhook URLs or full email addresses.
///   - Must NEVER store credentials, CredentialsJson, or SettingsJson.
///   - HTTP 2xx = success; 429/5xx/network timeout = retryable failure.
///   - HTTP 400/401/403 = non-retryable config/auth failure.
/// </summary>
public interface ISmsAlertEscalationChannelAdapter
{
    /// <summary>Returns true if this adapter handles the given channel type string.</summary>
    bool Supports(string channelType);

    /// <summary>
    /// Attempts delivery of the escalation payload to the configured channel target.
    /// Must not throw — all errors must be returned as <see cref="EscalationDeliveryResult.Failed"/>.
    /// </summary>
    Task<EscalationDeliveryResult> SendAsync(
        SmsOperationalEscalationPolicy policy,
        SmsOperationalAlert alert,
        EscalationPayload payload,
        CancellationToken ct);
}
