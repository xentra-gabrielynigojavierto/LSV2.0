namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-014: Persisted SMS routing decision for audit, debug, and reporting.
/// Created by SmsRoutingEngine before the send attempt. AttemptId linked post-send.
///
/// Security: No credentials, CredentialsJson, SettingsJson, auth tokens,
/// webhook URLs, or raw phone numbers stored in this entity.
/// ProviderConfigId is opaque Guid — no credential data.
/// CandidateProvidersJson/ExcludedProvidersJson contain provider type strings only.
/// </summary>
public class SmsRoutingDecision
{
    public Guid    Id             { get; set; } = Guid.NewGuid();
    public Guid?   TenantId       { get; set; }
    public Guid?   NotificationId { get; set; }

    /// <summary>Linked to NotificationAttempt.Id after send completes.</summary>
    public Guid?   AttemptId          { get; set; }

    /// <summary>Matched routing policy, if any.</summary>
    public Guid?   RoutingPolicyId    { get; set; }

    /// <summary>Routing mode used: priority | cost_optimized | health_optimized | hybrid | regional | no_route</summary>
    public string  RoutingMode        { get; set; } = string.Empty;

    public string  SelectedProvider   { get; set; } = string.Empty;
    public Guid?   SelectedProviderConfigId { get; set; }
    public string? ProviderOwnershipMode    { get; set; }

    /// <summary>JSON array of candidate provider type strings at decision time.</summary>
    public string? CandidateProvidersJson { get; set; }

    /// <summary>JSON array of excluded provider type strings.</summary>
    public string? ExcludedProvidersJson  { get; set; }

    /// <summary>Human-readable reason for selection or failure.</summary>
    public string  DecisionReason         { get; set; } = string.Empty;

    public decimal? EstimatedCostAmount   { get; set; }
    public string?  CostCurrency          { get; set; }

    /// <summary>Reserved for future health snapshot data (JSON).</summary>
    public string?  HealthSnapshotJson    { get; set; }

    public string?  Region      { get; set; }
    public string?  CountryCode { get; set; }

    // ── LS-NOTIF-SMS-015: Adaptive routing metadata ──────────────────────────
    /// <summary>Country code inferred from recipient phone via E.164 prefix. Never a raw phone number.</summary>
    public string?  InferredCountryCode  { get; set; }
    /// <summary>Region derived from InferredCountryCode (e.g., "NANP", "EU"). Never a raw phone number.</summary>
    public string?  InferredRegion       { get; set; }
    /// <summary>Quality score (0-100) of the selected provider at decision time. Null when not adaptive mode.</summary>
    public decimal? ProviderQualityScore { get; set; }
    /// <summary>Composite adaptive score used by adaptive_balanced mode. Null for non-adaptive modes.</summary>
    public decimal? AdaptiveScore        { get; set; }
    /// <summary>JSON object with adaptive routing inputs (quality/cost/latency inputs). Null for non-adaptive modes.</summary>
    public string?  AdaptiveInputsJson   { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
