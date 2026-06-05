namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-016: Configuration for recipient intelligence scoring and suppression.
/// Bound from appsettings section "SmsRecipientIntelligence".
/// </summary>
public class SmsRecipientIntelligenceOptions
{
    public const string SectionName = "SmsRecipientIntelligence";

    /// <summary>Enable recipient intelligence calculation worker. Default false (safe-off).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// HMAC salt for recipient phone hashing. REQUIRED for production.
    /// When empty, falls back to SHA256 with a fixed prefix (dev-only safety).
    /// Configure via environment secret: SmsRecipientIntelligence:RecipientHashSalt
    /// </summary>
    public string RecipientHashSalt { get; set; } = string.Empty;

    /// <summary>Lookback window in days for reputation snapshot calculation. Default 90.</summary>
    public int ReputationWindowDays { get; set; } = 90;

    /// <summary>Minimum attempt count to produce meaningful scores. Default 5.</summary>
    public int MinimumAttemptCount { get; set; } = 5;

    /// <summary>How often the background worker recalculates snapshots (minutes). Default 120.</summary>
    public int CalculationIntervalMinutes { get; set; } = 120;

    // ── Suppression thresholds (RetrySuppressionRisk, 0-100) ──────────────────

    /// <summary>
    /// RetrySuppressionRisk threshold above which hard suppression is applied.
    /// Hard suppress moves notification to dead-letter immediately. Default 80.
    /// </summary>
    public decimal HardSuppressionThreshold { get; set; } = 80m;

    /// <summary>
    /// RetrySuppressionRisk threshold above which soft suppression is applied
    /// (retry deferred 30 min, warn logged). Default 60.
    /// </summary>
    public decimal SoftSuppressionThreshold { get; set; } = 60m;

    /// <summary>
    /// RetrySuppressionRisk threshold above which a warn decision is recorded
    /// but delivery proceeds normally. Default 40.
    /// </summary>
    public decimal WarnSuppressionThreshold { get; set; } = 40m;

    /// <summary>
    /// InvalidNumberRisk threshold above which review_required is triggered
    /// (blocks automated retry, requires operator review). Default 85.
    /// </summary>
    public decimal InvalidNumberReviewThreshold { get; set; } = 85m;

    // ── Score weights ─────────────────────────────────────────────────────────

    /// <summary>Weight for delivery success when computing QualityScore. Default 0.50.</summary>
    public decimal DeliverySuccessWeight { get; set; } = 0.50m;

    /// <summary>Penalty weight for failure rate. Default 0.25.</summary>
    public decimal FailurePenaltyWeight { get; set; } = 0.25m;

    /// <summary>Penalty weight for dead-letter rate. Default 0.15.</summary>
    public decimal DeadLetterPenaltyWeight { get; set; } = 0.15m;

    /// <summary>Penalty weight for carrier failure rate. Default 0.10.</summary>
    public decimal CarrierFailurePenaltyWeight { get; set; } = 0.10m;

    /// <summary>Max number of recipient snapshots to process in one worker cycle. Default 5000.</summary>
    public int MaxSnapshotsPerCycle { get; set; } = 5_000;

    /// <summary>Max number of attempts loaded per calculation window. Default 100000.</summary>
    public int MaxAttemptsPerWindow { get; set; } = 100_000;
}
