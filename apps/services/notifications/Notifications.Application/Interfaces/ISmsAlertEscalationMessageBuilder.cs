using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-011: Builds a safe, channel-portable escalation payload from an
/// SmsOperationalAlert entity.
///
/// The payload MUST NOT include:
///   - phone numbers
///   - credentials or API keys
///   - raw provider payloads
///   - CredentialsJson, SettingsJson, or RecipientJson
///   - raw webhook URLs or escalation targets
///
/// The payload SHOULD include:
///   - alertId, alertType, severity
///   - tenantId, provider, providerConfigId (IDs/names only)
///   - metricValue, thresholdValue
///   - windowStart, windowEnd, occurrenceCount
///   - human-readable message
/// </summary>
public interface ISmsAlertEscalationMessageBuilder
{
    /// <summary>
    /// Builds a complete <see cref="EscalationPayload"/> from the given alert,
    /// including subject, body, Teams JSON, Slack JSON, and dedup hash.
    /// </summary>
    EscalationPayload Build(SmsOperationalAlert alert);
}
