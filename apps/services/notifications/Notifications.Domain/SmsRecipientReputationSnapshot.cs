namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-016: Recipient-level delivery reputation snapshot calculated from local operational telemetry.
/// Enables recipient intelligence, retry suppression heuristics, and destination risk scoring.
///
/// Security: No raw phone numbers stored. RecipientHash is HMAC-SHA256(normalizedPhone, salt) — a
/// deterministic, irreversible opaque token. CountryCode is a 2-char ISO code derived from E.164
/// prefix inference, never the raw phone. No credentials, CredentialsJson, SettingsJson,
/// ProviderMessageId, webhook URLs, or raw provider payloads stored.
/// All fields are aggregate operational metrics only.
/// </summary>
public class SmsRecipientReputationSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>HMAC-SHA256 of normalized phone. Never a raw phone number.</summary>
    public string RecipientHash { get; set; } = string.Empty;

    /// <summary>null = cross-tenant; set = tenant-scoped aggregate.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Provider type this snapshot covers, or null for cross-provider aggregate.</summary>
    public string? ProviderType { get; set; }

    /// <summary>ISO country code inferred from E.164 prefix. Never a raw phone number.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Routing region derived from country code. Never a raw phone number.</summary>
    public string? Region { get; set; }

    // ── Attempt counts ─────────────────────────────────────────────────────────
    public int TotalAttempts               { get; set; }
    public int DeliveredAttempts           { get; set; }
    public int FailedAttempts              { get; set; }
    public int RetryAttempts               { get; set; }
    public int DeadLetterAttempts          { get; set; }
    public int CarrierRejectedAttempts     { get; set; }
    public int InvalidDestinationAttempts  { get; set; }

    // ── Latency ────────────────────────────────────────────────────────────────
    /// <summary>Average delivery latency in milliseconds (CompletedAt - CreatedAt) for delivered attempts.</summary>
    public decimal? AverageLatencyMs { get; set; }

    // ── Rates (0–1) ────────────────────────────────────────────────────────────
    public decimal DeliverySuccessRate  { get; set; }
    public decimal FailureRate          { get; set; }
    public decimal RetryRate            { get; set; }
    public decimal DeadLetterRate       { get; set; }
    public decimal CarrierFailureRate   { get; set; }

    // ── Risk scores (0–100) ────────────────────────────────────────────────────
    /// <summary>
    /// Risk that this recipient hash represents an invalid/unreachable number. 0 = clean. 100 = high risk.
    /// Derived from weighted invalid_destination + carrier_rejected failure history.
    /// </summary>
    public decimal InvalidNumberRisk       { get; set; }

    /// <summary>
    /// Risk that retrying this recipient will fail again. 0 = safe. 100 = suppress.
    /// Derived from retry + dead-letter + repeated failure history.
    /// </summary>
    public decimal RetrySuppressionRisk    { get; set; }

    /// <summary>Overall delivery quality score 0–100. Higher = better.</summary>
    public decimal QualityScore            { get; set; }

    // ── Classification ─────────────────────────────────────────────────────────
    /// <summary>low | medium | high | suppressed</summary>
    public string DestinationRiskLevel { get; set; } = "low";

    // ── Timestamps ─────────────────────────────────────────────────────────────
    public DateTime? LastAttemptAt { get; set; }
    public DateTime  CalculatedAt  { get; set; } = DateTime.UtcNow;
}
