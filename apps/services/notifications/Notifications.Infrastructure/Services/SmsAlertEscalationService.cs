using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-011: Orchestrates SMS alert escalation — matches active alerts
/// to enabled policies, delivers via channel adapters, persists attempt history,
/// and handles dedup/cooldown and retry scheduling.
///
/// Constraints:
///   - Never triggers SMS sends.
///   - Never calls SMS providers.
///   - All external delivery failures are caught — the service never throws to callers.
///   - Disabled by default (SMS_ALERT_ESCALATION_ENABLED=false).
/// </summary>
public sealed class SmsAlertEscalationService : ISmsAlertEscalationService
{
    private readonly ISmsOperationalAlertRepository             _alertRepo;
    private readonly ISmsOperationalEscalationPolicyRepository  _policyRepo;
    private readonly ISmsOperationalAlertEscalationRepository   _escalationRepo;
    private readonly ISmsAlertEscalationMessageBuilder          _messageBuilder;
    private readonly IEnumerable<ISmsAlertEscalationChannelAdapter> _adapters;
    private readonly ILogger<SmsAlertEscalationService>         _logger;
    private readonly bool _enabled;
    private readonly int  _httpTimeoutSeconds;

    private const int MaxFailureReasonLength = 1000;
    private const int DefaultRetryDelayMinutes = 5;

    public SmsAlertEscalationService(
        ISmsOperationalAlertRepository             alertRepo,
        ISmsOperationalEscalationPolicyRepository  policyRepo,
        ISmsOperationalAlertEscalationRepository   escalationRepo,
        ISmsAlertEscalationMessageBuilder          messageBuilder,
        IEnumerable<ISmsAlertEscalationChannelAdapter> adapters,
        IConfiguration                             configuration,
        ILogger<SmsAlertEscalationService>         logger)
    {
        _alertRepo       = alertRepo;
        _policyRepo      = policyRepo;
        _escalationRepo  = escalationRepo;
        _messageBuilder  = messageBuilder;
        _adapters        = adapters;
        _logger          = logger;

        _enabled            = configuration.GetValue("SMS_ALERT_ESCALATION_ENABLED", false);
        _httpTimeoutSeconds = configuration.GetValue("SMS_ALERT_ESCALATION_HTTP_TIMEOUT_SECONDS", 10);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task EscalateAlertAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await _alertRepo.GetByIdAsync(alertId, ct);
        if (alert is null)
        {
            _logger.LogWarning(
                "SmsAlertEscalationService: alert {AlertId} not found — cannot escalate", alertId);
            return;
        }
        await EscalateAlertAsync(alert, ct);
    }

    public async Task EscalateAlertAsync(SmsOperationalAlert alert, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug(
                "SMS alert escalation is disabled (SMS_ALERT_ESCALATION_ENABLED=false); " +
                "skipping escalation for alert {AlertId}", alert.Id);
            return;
        }

        // Only escalate active (or suppressed-but-reactivating) alerts.
        if (alert.Status is not "active")
        {
            _logger.LogDebug(
                "Skipping escalation for alert {AlertId} with non-active status={Status}",
                alert.Id, alert.Status);
            return;
        }

        List<SmsOperationalEscalationPolicy> policies;
        try
        {
            policies = await _policyRepo.GetEnabledMatchingPoliciesAsync(alert, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsAlertEscalationService: failed to load policies for alert {AlertId}", alert.Id);
            return;
        }

        if (policies.Count == 0)
        {
            _logger.LogDebug(
                "No matching escalation policies for alert {AlertId} type={AlertType} severity={Severity}",
                alert.Id, alert.AlertType, alert.Severity);
            return;
        }

        _logger.LogInformation(
            "SmsAlertEscalationService: escalating alert {AlertId} type={AlertType} severity={Severity} " +
            "via {PolicyCount} matching policies",
            alert.Id, alert.AlertType, alert.Severity, policies.Count);

        foreach (var policy in policies)
        {
            await EscalateWithPolicyAsync(alert, policy, ct);
        }
    }

    public async Task<bool> RetryEscalationAsync(
        Guid escalationId, string? requestedBy, CancellationToken ct = default)
    {
        var escalation = await _escalationRepo.GetByIdAsync(escalationId, ct);
        if (escalation is null)
        {
            _logger.LogWarning(
                "RetryEscalationAsync: escalation {Id} not found", escalationId);
            return false;
        }

        if (escalation.Status is "sent" or "suppressed" or "skipped")
        {
            _logger.LogInformation(
                "RetryEscalationAsync: escalation {Id} is {Status} — not eligible for retry",
                escalationId, escalation.Status);
            return false;
        }

        // Load policy for channel/target info.
        SmsOperationalEscalationPolicy? policy = null;
        if (escalation.PolicyId.HasValue)
            policy = await _policyRepo.GetByIdAsync(escalation.PolicyId.Value, ct);

        if (policy is null)
        {
            _logger.LogWarning(
                "RetryEscalationAsync: escalation {Id} has no policy — cannot retry", escalationId);
            escalation.Status        = "failed";
            escalation.FailureReason = "Policy no longer exists; cannot retry.";
            await _escalationRepo.UpdateAsync(escalation, ct);
            return false;
        }

        var alert = await _alertRepo.GetByIdAsync(escalation.AlertId, ct);
        if (alert is null)
        {
            _logger.LogWarning(
                "RetryEscalationAsync: alert {AlertId} not found for escalation {Id}",
                escalation.AlertId, escalationId);
            return false;
        }

        await AttemptDeliveryAsync(escalation, policy, alert, ct);
        return true;
    }

    public async Task<int> ProcessPendingRetriesAsync(int limit, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug(
                "SMS alert escalation is disabled; skipping pending retry processing");
            return 0;
        }

        List<SmsOperationalAlertEscalation> due;
        try
        {
            due = await _escalationRepo.GetPendingRetriesAsync(limit, DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmsAlertEscalationService: failed to load pending retries");
            return 0;
        }

        if (due.Count == 0) return 0;

        _logger.LogInformation(
            "SmsAlertEscalationService: processing {Count} pending retry escalations", due.Count);

        var processed = 0;
        foreach (var escalation in due)
        {
            try
            {
                if (!escalation.PolicyId.HasValue) continue;
                var policy = await _policyRepo.GetByIdAsync(escalation.PolicyId.Value, ct);
                if (policy is null) continue;

                var alert = await _alertRepo.GetByIdAsync(escalation.AlertId, ct);
                if (alert is null) continue;

                await AttemptDeliveryAsync(escalation, policy, alert, ct);
                processed++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SmsAlertEscalationService: error retrying escalation {Id}", escalation.Id);
            }
        }

        return processed;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task EscalateWithPolicyAsync(
        SmsOperationalAlert alert,
        SmsOperationalEscalationPolicy policy,
        CancellationToken ct)
    {
        try
        {
            // Build payload.
            EscalationPayload payload;
            try
            {
                payload = _messageBuilder.Build(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SmsAlertEscalationService: message builder threw for alert {AlertId}", alert.Id);
                return;
            }

            // Dedup check.
            var duplicate = await _escalationRepo.FindRecentDuplicateAsync(
                alert.Id, policy.Id, payload.PayloadHash, policy.CooldownMinutes, ct);

            if (duplicate is not null)
            {
                _logger.LogDebug(
                    "Escalation suppressed by cooldown: alert={AlertId} policy={PolicyId} " +
                    "existing={ExistingId} status={ExistingStatus}",
                    alert.Id, policy.Id, duplicate.Id, duplicate.Status);

                var suppressed = BuildEscalationRecord(alert, policy, payload, "suppressed");
                suppressed.SuppressedUntil = DateTime.UtcNow.AddMinutes(policy.CooldownMinutes);
                await _escalationRepo.CreateAsync(suppressed, ct);
                return;
            }

            // Create pending record.
            var escalation = BuildEscalationRecord(alert, policy, payload, "pending");
            await _escalationRepo.CreateAsync(escalation, ct);

            // Find adapter.
            var adapter = _adapters.FirstOrDefault(a => a.Supports(policy.ChannelType));
            if (adapter is null)
            {
                _logger.LogWarning(
                    "No escalation adapter registered for channel type '{ChannelType}'", policy.ChannelType);
                escalation.Status        = "skipped";
                escalation.FailureReason = $"No adapter registered for channel type '{policy.ChannelType}'.";
                await _escalationRepo.UpdateAsync(escalation, ct);
                return;
            }

            // Deliver.
            await AttemptDeliveryAsync(escalation, policy, alert, ct, payload);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsAlertEscalationService: unhandled error escalating alert {AlertId} " +
                "via policy {PolicyId}", alert.Id, policy.Id);
        }
    }

    private async Task AttemptDeliveryAsync(
        SmsOperationalAlertEscalation escalation,
        SmsOperationalEscalationPolicy policy,
        SmsOperationalAlert alert,
        CancellationToken ct,
        EscalationPayload? payload = null)
    {
        if (payload is null)
        {
            try
            {
                payload = _messageBuilder.Build(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AttemptDelivery: message builder threw for alert {AlertId}", alert.Id);
                escalation.Status        = "failed";
                escalation.FailureReason = "Message builder failed.";
                escalation.UpdatedAt     = DateTime.UtcNow;
                await _escalationRepo.UpdateAsync(escalation, ct);
                return;
            }
        }

        var adapter = _adapters.FirstOrDefault(a => a.Supports(policy.ChannelType));
        if (adapter is null)
        {
            escalation.Status        = "skipped";
            escalation.FailureReason = $"No adapter for '{policy.ChannelType}'.";
            escalation.UpdatedAt     = DateTime.UtcNow;
            await _escalationRepo.UpdateAsync(escalation, ct);
            return;
        }

        escalation.AttemptCount++;
        escalation.LastAttemptAt  = DateTime.UtcNow;
        escalation.NextRetryAt    = null;

        EscalationDeliveryResult result;
        try
        {
            result = await adapter.SendAsync(policy, alert, payload, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AttemptDelivery: adapter threw for alert {AlertId} channel {Channel}",
                alert.Id, policy.ChannelType);
            result = EscalationDeliveryResult.Failed("adapter_exception", ex.Message, retryable: true);
        }

        if (result.Success)
        {
            escalation.Status = "sent";
            escalation.SentAt = DateTime.UtcNow;
            escalation.FailureReason = null;

            if (!string.IsNullOrWhiteSpace(result.ExternalMessageId))
                escalation.MetadataJson = $"{{\"externalMessageId\":\"{result.ExternalMessageId}\"}}";
        }
        else
        {
            var reason = result.ErrorMessage;
            escalation.FailureReason = reason is null
                ? null
                : reason[..Math.Min(reason.Length, MaxFailureReasonLength)];

            if (result.Retryable && policy.RetryEnabled && escalation.AttemptCount <= policy.MaxRetryCount)
            {
                escalation.Status      = "pending";
                escalation.NextRetryAt = DateTime.UtcNow.AddMinutes(DefaultRetryDelayMinutes);
            }
            else
            {
                escalation.Status = "failed";
            }
        }

        escalation.UpdatedAt = DateTime.UtcNow;
        await _escalationRepo.UpdateAsync(escalation, ct);
    }

    private static SmsOperationalAlertEscalation BuildEscalationRecord(
        SmsOperationalAlert alert,
        SmsOperationalEscalationPolicy policy,
        EscalationPayload payload,
        string status)
    {
        var now = DateTime.UtcNow;
        return new SmsOperationalAlertEscalation
        {
            Id           = Guid.NewGuid(),
            AlertId      = alert.Id,
            PolicyId     = policy.Id,
            ChannelType  = policy.ChannelType,
            TargetMasked = SmsEscalationPolicyRepository.MaskTarget(policy.Target, policy.ChannelType),
            Severity     = alert.Severity,
            Status       = status,
            AttemptCount = 0,
            PayloadHash  = payload.PayloadHash,
            CreatedAt    = now,
            UpdatedAt    = now,
        };
    }
}
