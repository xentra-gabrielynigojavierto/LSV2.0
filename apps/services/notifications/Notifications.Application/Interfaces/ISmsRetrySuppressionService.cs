namespace Notifications.Application.Interfaces;

/// <summary>LS-NOTIF-SMS-016: Input for suppression evaluation.</summary>
public sealed class SmsRetrySuppressionRequest
{
    /// <summary>Raw phone number (will be normalized + hashed internally). Never persisted as-is.</summary>
    public string? RecipientPhone { get; set; }

    public Guid?   TenantId        { get; set; }
    public Guid?   NotificationId  { get; set; }
    public Guid?   AttemptId       { get; set; }
    public string? ProviderType    { get; set; }
    public string? CountryCode     { get; set; }
    public string? Region          { get; set; }
    public int     RetryCount      { get; set; }
    public string? FailureCategory { get; set; }
}

/// <summary>LS-NOTIF-SMS-016: Output of suppression evaluation.</summary>
public sealed class SmsRetrySuppressionResult
{
    /// <summary>allow | warn | soft_suppress | hard_suppress | review_required</summary>
    public string DecisionType { get; set; } = "allow";

    /// <summary>Reason code explaining the decision.</summary>
    public string ReasonCode { get; set; } = "telemetry_unavailable";

    /// <summary>RetrySuppressionRisk score at decision time. Null if no snapshot.</summary>
    public decimal? RiskScore { get; set; }

    /// <summary>QualityScore at decision time. Null if no snapshot.</summary>
    public decimal? QualityScore { get; set; }

    public bool ShouldBlock    => DecisionType is "hard_suppress" or "review_required";
    public bool ShouldDefer    => DecisionType == "soft_suppress";
    public bool ShouldProceed  => DecisionType is "allow" or "warn";
}

/// <summary>
/// LS-NOTIF-SMS-016: Retry suppression evaluation service.
/// Integrates with recipient intelligence to decide whether a retry should proceed.
///
/// Safe degradation: always returns "allow" when telemetry insufficient or service throws.
/// Never crashes the delivery pipeline.
/// </summary>
public interface ISmsRetrySuppressionService
{
    /// <summary>
    /// Evaluate whether a retry/send should proceed for the given recipient.
    /// Hashes the phone internally — raw phone never stored.
    /// Returns "allow" when telemetry is insufficient to make a confident decision.
    /// </summary>
    Task<SmsRetrySuppressionResult> EvaluateAsync(
        SmsRetrySuppressionRequest request,
        CancellationToken ct);
}
