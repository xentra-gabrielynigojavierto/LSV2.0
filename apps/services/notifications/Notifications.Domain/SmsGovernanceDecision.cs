namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-017: Governance decision audit record.
/// Persisted when a governance policy affects delivery behavior (delay/throttle/block/review_required)
/// or for audit purposes when allow is noteworthy (e.g. emergency override).
///
/// Raw phone numbers are NEVER stored here.
/// Provider credentials, SettingsJson, CredentialsJson, and webhook URLs are NEVER stored here.
/// DecisionMetadataJson contains safe operational aggregate fields only.
/// </summary>
public sealed class SmsGovernanceDecision
{
    public Guid    Id             { get; set; } = Guid.NewGuid();
    public Guid?   NotificationId { get; set; }
    public Guid?   AttemptId      { get; set; }
    public Guid?   TenantId       { get; set; }
    public Guid?   PolicyId       { get; set; }

    /// <summary>quiet_hours | geographic_restriction | rate_limit | provider_governance | retry_governance | escalation_guardrail | no_applicable_policy</summary>
    public string  PolicyType     { get; set; } = string.Empty;

    /// <summary>allow | delay | throttle | block | review_required | override_allowed</summary>
    public string  DecisionType   { get; set; } = "allow";

    /// <summary>
    /// quiet_hours_active | geographic_blocked | geographic_not_allowed |
    /// tenant_rate_limit_exceeded | provider_blocked | provider_not_allowed |
    /// retry_limit_exceeded | escalation_rate_limit_exceeded |
    /// no_applicable_policy | policy_evaluation_error | emergency_override
    /// </summary>
    public string  ReasonCode     { get; set; } = string.Empty;

    public string? ProviderType       { get; set; }
    public Guid?   ProviderConfigId   { get; set; }
    public string? CountryCode        { get; set; }
    public string? Region             { get; set; }

    /// <summary>When a delay/throttle decision resolves — the next-allowed send time.</summary>
    public DateTime? EffectiveAt      { get; set; }

    /// <summary>Safe operational metadata JSON — no phone numbers, no credentials.</summary>
    public string?   DecisionMetadataJson { get; set; }

    public DateTime  CreatedAt         { get; set; } = DateTime.UtcNow;
}
