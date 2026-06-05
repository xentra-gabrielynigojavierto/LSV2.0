namespace Notifications.Application.Interfaces;

// ─── Request ─────────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-017: Input for governance policy evaluation.</summary>
public sealed class SmsGovernanceEvaluationRequest
{
    public Guid?   TenantId                  { get; set; }
    public Guid?   NotificationId            { get; set; }
    public Guid?   AttemptId                 { get; set; }

    /// <summary>
    /// Raw phone number — used ONLY transiently for country inference.
    /// Never persisted as-is in governance tables.
    /// </summary>
    public string? RecipientPhoneTransient   { get; set; }

    public string? ProviderType             { get; set; }
    public Guid?   ProviderConfigId         { get; set; }
    public int     RetryCount               { get; set; }
    public bool    IsRetry                  { get; set; }
    public bool    IsEscalation             { get; set; }
    public bool    IsEmergencyOverride      { get; set; }
    public string? AlertType                { get; set; }
    public DateTime NowUtc                  { get; set; } = DateTime.UtcNow;
}

// ─── Result ──────────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-017: Output of governance policy evaluation.</summary>
public sealed class SmsGovernanceEvaluationResult
{
    /// <summary>allow | delay | throttle | block | review_required | override_allowed</summary>
    public string  DecisionType        { get; set; } = "allow";

    /// <summary>Reason code explaining the decision.</summary>
    public string  ReasonCode          { get; set; } = "no_applicable_policy";

    /// <summary>The policy that triggered this decision, if any.</summary>
    public Guid?   PolicyId            { get; set; }
    public string? PolicyName          { get; set; }
    public string? PolicyType          { get; set; }

    /// <summary>Next-allowed send time for delay/throttle decisions.</summary>
    public DateTime? EffectiveAt       { get; set; }

    /// <summary>Country code inferred during geographic evaluation (transient — not persisted as phone).</summary>
    public string? CountryCode         { get; set; }
    public string? Region              { get; set; }

    /// <summary>Safe operational metadata (no phone, no credentials).</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    // ─── Convenience booleans ──────────────────────────────────────────────
    public bool ShouldProceed  => DecisionType is "allow" or "override_allowed";
    public bool ShouldDelay    => DecisionType == "delay";
    public bool ShouldThrottle => DecisionType == "throttle";
    public bool ShouldBlock    => DecisionType is "block" or "review_required";
}

// ─── Service interface ────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-017: Central SMS governance policy evaluation service.
///
/// Uses local Notification Service data only — no external API calls.
/// Phone numbers are used transiently for country inference and never persisted.
/// Evaluation failures fail-open (return allow) by default (configurable).
/// Disabled policies are never evaluated.
/// If no policy applies, returns allow/no_applicable_policy.
/// </summary>
public interface ISmsGovernancePolicyService
{
    /// <summary>
    /// Evaluate governance before an initial SMS send.
    /// Covers: quiet_hours, geographic_restriction, rate_limit, provider_governance.
    /// </summary>
    Task<SmsGovernanceEvaluationResult> EvaluatePreSendAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate governance before an automated retry.
    /// Covers: quiet_hours, rate_limit, provider_governance, retry_governance.
    /// </summary>
    Task<SmsGovernanceEvaluationResult> EvaluateRetryAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate governance before an alert escalation delivery.
    /// Covers: escalation_guardrail.
    /// </summary>
    Task<SmsGovernanceEvaluationResult> EvaluateEscalationAsync(
        SmsGovernanceEvaluationRequest request,
        CancellationToken ct = default);
}
