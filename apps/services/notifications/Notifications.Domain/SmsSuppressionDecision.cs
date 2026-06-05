namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-016: Persisted suppression decision for auditability.
/// Records the outcome of ISmsRetrySuppressionService.EvaluateAsync for each suppression evaluation.
///
/// Security: No raw phone numbers. RecipientHash is an opaque HMAC-SHA256 token only.
/// DecisionMetadataJson must never contain credentials, provider credentials, or raw phone numbers.
/// </summary>
public class SmsSuppressionDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>HMAC-SHA256 of normalized phone. Never a raw phone number.</summary>
    public string RecipientHash { get; set; } = string.Empty;

    public Guid? TenantId       { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? AttemptId      { get; set; }

    /// <summary>allow | warn | soft_suppress | hard_suppress | review_required</summary>
    public string DecisionType { get; set; } = "allow";

    /// <summary>
    /// invalid_destination | excessive_failures | excessive_retries | carrier_rejections |
    /// dead_letter_history | insufficient_quality | manual_override | telemetry_unavailable
    /// </summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Retry suppression risk score at decision time (0–100). Null when telemetry insufficient.</summary>
    public decimal? RiskScore    { get; set; }

    /// <summary>Quality score at decision time (0–100). Null when telemetry insufficient.</summary>
    public decimal? QualityScore { get; set; }

    /// <summary>Retry count on the notification at decision time.</summary>
    public int RetryCount { get; set; }

    public string? ProviderType { get; set; }
    public string? CountryCode  { get; set; }
    public string? Region       { get; set; }

    /// <summary>Safe operational metadata only. Never credentials or phone numbers.</summary>
    public string? DecisionMetadataJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
