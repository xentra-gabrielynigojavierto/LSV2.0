namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-015: Persisted provider quality snapshot calculated from local operational telemetry.
/// Created by SmsProviderQualityService / SmsProviderQualityWorker from NotificationAttempt data.
///
/// Security: No phone numbers, recipient data, CredentialsJson, SettingsJson,
/// auth tokens, webhook URLs, or raw provider payloads stored in this entity.
/// ProviderConfigId is an opaque Guid — no credential data.
/// All fields are aggregate operational metrics only.
/// </summary>
public class SmsProviderQualitySnapshot
{
    public Guid   Id                  { get; set; } = Guid.NewGuid();
    public string ProviderType        { get; set; } = string.Empty;

    /// <summary>Opaque reference to a TenantProviderConfiguration. Never contains credentials.</summary>
    public Guid?  ProviderConfigId    { get; set; }
    public string? ProviderOwnershipMode { get; set; }

    /// <summary>Null = platform-wide aggregate; set = tenant-scoped aggregate.</summary>
    public Guid?  TenantId            { get; set; }

    /// <summary>Approximate region inferred from delivery geography (e.g., "US", "EU"). Never a phone number.</summary>
    public string? Region             { get; set; }

    /// <summary>ISO country code inferred from E.164 prefix (e.g., "US", "GB"). Never a phone number.</summary>
    public string? CountryCode        { get; set; }

    public DateTime WindowStart        { get; set; }
    public DateTime WindowEnd          { get; set; }

    // ── Attempt counts ──────────────────────────────────────────────────────
    public int TotalAttempts           { get; set; }
    public int DeliveredAttempts       { get; set; }
    public int FailedAttempts          { get; set; }
    public int RetryAttempts           { get; set; }
    public int DeadLetterAttempts      { get; set; }
    public int ReconciledAttempts      { get; set; }
    public int ReconciliationFailures  { get; set; }

    // ── Latency (ms) ────────────────────────────────────────────────────────
    /// <summary>Average (CompletedAt - CreatedAt) in milliseconds, where both are non-null.</summary>
    public decimal? AverageLatencyMs   { get; set; }

    // ── Rates (0-1) ─────────────────────────────────────────────────────────
    public decimal DeliverySuccessRate        { get; set; }
    public decimal FailureRate                { get; set; }
    public decimal RetryRate                  { get; set; }
    public decimal DeadLetterRate             { get; set; }
    public decimal ReconciliationFailureRate  { get; set; }

    // ── Cost ────────────────────────────────────────────────────────────────
    /// <summary>Average of ActualCostAmount ?? EstimatedCostAmount per attempt.</summary>
    public decimal? AverageEffectiveCost        { get; set; }
    /// <summary>Total effective cost / DeliveredAttempts. Null if no deliveries.</summary>
    public decimal? CostPerDeliveredMessage     { get; set; }

    // ── Scores (0-100) ──────────────────────────────────────────────────────
    /// <summary>Composite quality score 0-100 per SmsProviderQualityOptions weights.</summary>
    public decimal QualityScore                 { get; set; }
    /// <summary>Normalized cost-efficiency score 0-100. Higher = more cost-efficient. Null if cost unavailable.</summary>
    public decimal? CostEfficiencyScore         { get; set; }
    /// <summary>Health penalty input (0-1): 0 = healthy, 0.5 = degraded, 1.0 = down.</summary>
    public decimal HealthPenalty                { get; set; }

    public DateTime CalculatedAt               { get; set; } = DateTime.UtcNow;
}
