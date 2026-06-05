using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

// ── Internal / Email adapter ─────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-011: Escalation adapter for "internal_notification" and "email" channel types.
///
/// Uses the existing IEmailProviderAdapter (SendGrid) to deliver a plain-text email
/// to the address stored in policy.Target.
///
/// Constraints:
///   - Never sends SMS.
///   - Never calls any SMS provider.
///   - Target is treated as an email address.
///   - Full Target is never logged — only the masked form is referenced.
/// </summary>
public sealed class InternalEmailEscalationAdapter : ISmsAlertEscalationChannelAdapter
{
    private readonly IEmailProviderAdapter _email;
    private readonly ILogger<InternalEmailEscalationAdapter> _logger;

    public InternalEmailEscalationAdapter(
        IEmailProviderAdapter email,
        ILogger<InternalEmailEscalationAdapter> logger)
    {
        _email  = email;
        _logger = logger;
    }

    public bool Supports(string channelType) =>
        channelType is "internal_notification" or "email";

    public async Task<EscalationDeliveryResult> SendAsync(
        SmsOperationalEscalationPolicy policy,
        SmsOperationalAlert alert,
        EscalationPayload payload,
        CancellationToken ct)
    {
        try
        {
            var result = await _email.SendAsync(new EmailSendPayload
            {
                To      = policy.Target,
                Subject = payload.Subject,
                Body    = payload.Body,
                Html    = null,
            });

            if (result.Success)
            {
                _logger.LogInformation(
                    "SMS escalation email sent for alert {AlertId} severity={Severity}",
                    alert.Id, alert.Severity);

                return EscalationDeliveryResult.Succeeded(result.ProviderMessageId);
            }

            var failure = result.Failure;
            _logger.LogWarning(
                "SMS escalation email failed for alert {AlertId}: {Error} retryable={Retryable}",
                alert.Id, failure?.Message, failure?.Retryable);

            return EscalationDeliveryResult.Failed(
                failure?.Category ?? "email_failure",
                failure?.Message  ?? "Email send failed",
                failure?.Retryable ?? true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMS escalation email adapter threw for alert {AlertId}", alert.Id);

            return EscalationDeliveryResult.Failed("exception", ex.Message, retryable: true);
        }
    }
}

// ── Teams webhook adapter ─────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-011: Escalation adapter for "teams_webhook" channel type.
///
/// POSTs a Teams MessageCard JSON to the configured webhook URL.
/// The raw webhook URL (policy.Target) is NEVER logged.
///
/// HTTP response semantics:
///   2xx = success
///   429/5xx/network timeout = retryable failure
///   400/401/403 = non-retryable config/auth failure
/// </summary>
public sealed class TeamsWebhookEscalationAdapter : ISmsAlertEscalationChannelAdapter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TeamsWebhookEscalationAdapter> _logger;
    private readonly int _timeoutSeconds;

    public TeamsWebhookEscalationAdapter(
        IHttpClientFactory httpFactory,
        ILogger<TeamsWebhookEscalationAdapter> logger,
        int timeoutSeconds = 10)
    {
        _httpFactory    = httpFactory;
        _logger         = logger;
        _timeoutSeconds = timeoutSeconds;
    }

    public bool Supports(string channelType) => channelType == "teams_webhook";

    public async Task<EscalationDeliveryResult> SendAsync(
        SmsOperationalEscalationPolicy policy,
        SmsOperationalAlert alert,
        EscalationPayload payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.TeamsPayloadJson))
            return EscalationDeliveryResult.Failed("no_payload", "Teams payload is empty", retryable: false);

        try
        {
            using var client  = _httpFactory.CreateClient("EscalationWebhook");
            using var cts     = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            using var content = new StringContent(
                payload.TeamsPayloadJson,
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(policy.Target, content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Teams escalation sent for alert {AlertId} severity={Severity}",
                    alert.Id, alert.Severity);
                return EscalationDeliveryResult.Succeeded();
            }

            var statusCode = (int)response.StatusCode;
            var retryable  = statusCode == 429 || statusCode >= 500;

            _logger.LogWarning(
                "Teams escalation HTTP {StatusCode} for alert {AlertId} retryable={Retryable}",
                statusCode, alert.Id, retryable);

            return EscalationDeliveryResult.Failed(
                $"http_{statusCode}",
                $"Teams webhook returned HTTP {statusCode}",
                retryable);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Teams escalation timed out for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("timeout", "Request timed out", retryable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Teams escalation network error for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("network_error", ex.Message, retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Teams escalation adapter threw for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("exception", ex.Message, retryable: true);
        }
    }
}

// ── Slack webhook adapter ─────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-011: Escalation adapter for "slack_webhook" channel type.
///
/// POSTs a Slack Block Kit JSON to the configured incoming webhook URL.
/// The raw webhook URL (policy.Target) is NEVER logged.
///
/// HTTP response semantics:
///   2xx = success (Slack returns "ok" as body on success)
///   429/5xx/network timeout = retryable failure
///   400/401/403 = non-retryable config/auth failure
/// </summary>
public sealed class SlackWebhookEscalationAdapter : ISmsAlertEscalationChannelAdapter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SlackWebhookEscalationAdapter> _logger;
    private readonly int _timeoutSeconds;

    public SlackWebhookEscalationAdapter(
        IHttpClientFactory httpFactory,
        ILogger<SlackWebhookEscalationAdapter> logger,
        int timeoutSeconds = 10)
    {
        _httpFactory    = httpFactory;
        _logger         = logger;
        _timeoutSeconds = timeoutSeconds;
    }

    public bool Supports(string channelType) => channelType == "slack_webhook";

    public async Task<EscalationDeliveryResult> SendAsync(
        SmsOperationalEscalationPolicy policy,
        SmsOperationalAlert alert,
        EscalationPayload payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.SlackPayloadJson))
            return EscalationDeliveryResult.Failed("no_payload", "Slack payload is empty", retryable: false);

        try
        {
            using var client  = _httpFactory.CreateClient("EscalationWebhook");
            using var cts     = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            using var content = new StringContent(
                payload.SlackPayloadJson,
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(policy.Target, content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Slack escalation sent for alert {AlertId} severity={Severity}",
                    alert.Id, alert.Severity);
                return EscalationDeliveryResult.Succeeded();
            }

            var statusCode = (int)response.StatusCode;
            var retryable  = statusCode == 429 || statusCode >= 500;

            _logger.LogWarning(
                "Slack escalation HTTP {StatusCode} for alert {AlertId} retryable={Retryable}",
                statusCode, alert.Id, retryable);

            return EscalationDeliveryResult.Failed(
                $"http_{statusCode}",
                $"Slack webhook returned HTTP {statusCode}",
                retryable);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Slack escalation timed out for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("timeout", "Request timed out", retryable: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Slack escalation network error for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("network_error", ex.Message, retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Slack escalation adapter threw for alert {AlertId}", alert.Id);
            return EscalationDeliveryResult.Failed("exception", ex.Message, retryable: true);
        }
    }
}
