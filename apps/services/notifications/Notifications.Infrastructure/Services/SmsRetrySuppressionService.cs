using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-016: Evaluates whether a retry/send should proceed for a given recipient.
/// Uses local recipient reputation snapshots — no external API calls.
///
/// Safe degradation: returns "allow" when telemetry insufficient, salt unconfigured,
/// or any unexpected exception occurs. Never crashes the delivery pipeline.
///
/// Security: Phone is normalized and hashed internally. Raw phone never stored, logged,
/// or returned. Suppression decisions persist only the opaque RecipientHash.
/// </summary>
public class SmsRetrySuppressionService : ISmsRetrySuppressionService
{
    private readonly ISmsRecipientIdentityHasher        _hasher;
    private readonly ISmsRecipientIntelligenceService   _intelligenceService;
    private readonly SmsRecipientIntelligenceOptions    _opts;
    private readonly ILogger<SmsRetrySuppressionService> _logger;

    // Internal carrier failure categories that raise InvalidNumberRisk
    private static readonly HashSet<string> InvalidDestinationCategories =
        new(StringComparer.OrdinalIgnoreCase)
        { "invalid_recipient", "invalid_destination" };

    public SmsRetrySuppressionService(
        ISmsRecipientIdentityHasher       hasher,
        ISmsRecipientIntelligenceService  intelligenceService,
        IOptions<SmsRecipientIntelligenceOptions> opts,
        ILogger<SmsRetrySuppressionService> logger)
    {
        _hasher              = hasher;
        _intelligenceService = intelligenceService;
        _opts                = opts.Value;
        _logger              = logger;
    }

    public async Task<SmsRetrySuppressionResult> EvaluateAsync(
        SmsRetrySuppressionRequest request,
        CancellationToken ct)
    {
        try
        {
            var recipientHash = _hasher.HashRecipient(request.RecipientPhone);
            if (string.IsNullOrEmpty(recipientHash))
                return Allow("telemetry_unavailable");

            var snapshot = await _intelligenceService.GetRecipientSnapshotAsync(
                recipientHash, request.TenantId, ct);

            // No snapshot = insufficient telemetry → default to allow
            if (snapshot == null || snapshot.TotalAttempts < _opts.MinimumAttemptCount)
                return Allow("telemetry_unavailable");

            var risk    = snapshot.RetrySuppressionRisk;
            var invRisk = snapshot.InvalidNumberRisk;

            // Hard suppress: very high retry suppression risk
            if (risk >= _opts.HardSuppressionThreshold)
            {
                var decision = Decide("hard_suppress", "excessive_retries", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            // Review required: high invalid-number risk
            if (invRisk >= _opts.InvalidNumberReviewThreshold)
            {
                var decision = Decide("review_required", "invalid_destination", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            // Soft suppress: elevated retry risk
            if (risk >= _opts.SoftSuppressionThreshold)
            {
                var decision = Decide("soft_suppress", "excessive_retries", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            // Warn: approaching threshold
            if (risk >= _opts.WarnSuppressionThreshold)
            {
                var decision = Decide("warn", "insufficient_quality", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            // Check carrier rejection history separately
            if (snapshot.CarrierFailureRate >= 0.7m && snapshot.TotalAttempts >= _opts.MinimumAttemptCount)
            {
                var decision = Decide("soft_suppress", "carrier_rejections", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            // Also hard suppress on dead-letter history with current failure category being invalid
            if (!string.IsNullOrEmpty(request.FailureCategory) &&
                InvalidDestinationCategories.Contains(request.FailureCategory) &&
                snapshot.DeadLetterAttempts >= 2 &&
                snapshot.InvalidNumberRisk >= 70m)
            {
                var decision = Decide("hard_suppress", "dead_letter_history", risk, snapshot.QualityScore);
                await PersistAsync(recipientHash, request, decision, snapshot, ct);
                return decision;
            }

            return Allow("allow");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsRetrySuppressionService: evaluation threw for notification {NotificationId} — defaulting to allow",
                request.NotificationId);
            return Allow("telemetry_unavailable");
        }
    }

    private async Task PersistAsync(
        string                      recipientHash,
        SmsRetrySuppressionRequest  request,
        SmsRetrySuppressionResult   result,
        SmsRecipientReputationSnapshot snapshot,
        CancellationToken ct)
    {
        try
        {
            var meta = JsonSerializer.Serialize(new
            {
                risk_score           = result.RiskScore,
                quality_score        = result.QualityScore,
                total_attempts       = snapshot.TotalAttempts,
                carrier_failure_rate = snapshot.CarrierFailureRate,
                dead_letter_attempts = snapshot.DeadLetterAttempts,
                // No phone, no credentials
            });

            var decision = new SmsSuppressionDecision
            {
                RecipientHash       = recipientHash,
                TenantId            = request.TenantId,
                NotificationId      = request.NotificationId,
                AttemptId           = request.AttemptId,
                DecisionType        = result.DecisionType,
                ReasonCode          = result.ReasonCode,
                RiskScore           = result.RiskScore,
                QualityScore        = result.QualityScore,
                RetryCount          = request.RetryCount,
                ProviderType        = request.ProviderType,
                CountryCode         = request.CountryCode,
                Region              = request.Region,
                DecisionMetadataJson = meta,
                CreatedAt           = DateTime.UtcNow,
            };

            await _intelligenceService.PersistSuppressionDecisionAsync(decision, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsRetrySuppressionService: failed to persist suppression decision — continuing");
        }
    }

    private static SmsRetrySuppressionResult Allow(string reason) =>
        new() { DecisionType = "allow", ReasonCode = reason };

    private static SmsRetrySuppressionResult Decide(
        string decisionType,
        string reasonCode,
        decimal riskScore,
        decimal qualityScore) =>
        new()
        {
            DecisionType = decisionType,
            ReasonCode   = reasonCode,
            RiskScore    = riskScore,
            QualityScore = qualityScore,
        };
}
