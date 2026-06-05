namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-015: Configuration for provider quality scoring.
/// Bound from appsettings section "SmsProviderQuality".
/// </summary>
public class SmsProviderQualityOptions
{
    public const string SectionName = "SmsProviderQuality";

    /// <summary>Enable quality snapshot calculation. Default false (safe-off).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Lookback window for snapshot calculation in minutes. Default 1440 (24 h).</summary>
    public int SnapshotWindowMinutes { get; set; } = 1440;

    /// <summary>How often the background worker recalculates snapshots (minutes). Default 60.</summary>
    public int CalculationIntervalMinutes { get; set; } = 60;

    /// <summary>Minimum attempts required in the window to produce a meaningful score.
    /// Below this threshold, InsufficientDataScore is returned.</summary>
    public int MinimumAttemptCount { get; set; } = 20;

    // ── Score weight configuration ───────────────────────────────────────────
    // Weights are multipliers applied to normalised 0-1 rates when computing QualityScore.
    // They need not sum to 1 — the raw value is then clamped to [0, 100].

    /// <summary>Reward weight for delivery success rate (0-1). Default 0.45.</summary>
    public decimal DeliverySuccessWeight { get; set; } = 0.45m;

    /// <summary>Penalty weight for failure rate (0-1). Default 0.25.</summary>
    public decimal FailurePenaltyWeight { get; set; } = 0.25m;

    /// <summary>Penalty weight for retry rate (0-1). Default 0.10.</summary>
    public decimal RetryPenaltyWeight { get; set; } = 0.10m;

    /// <summary>Penalty weight for reconciliation failure rate (0-1). Default 0.10.</summary>
    public decimal ReconciliationPenaltyWeight { get; set; } = 0.10m;

    /// <summary>Weight for health penalty input (0-1 scalar). Default 0.10.</summary>
    public decimal HealthPenaltyWeight { get; set; } = 0.10m;

    /// <summary>Score returned when telemetry is absent (no attempts at all). Default 50.</summary>
    public decimal DefaultQualityScore { get; set; } = 50m;

    /// <summary>Score returned when attempts < MinimumAttemptCount. Default 50.</summary>
    public decimal InsufficientDataScore { get; set; } = 50m;
}
