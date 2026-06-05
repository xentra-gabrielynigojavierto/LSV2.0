using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>LS-NOTIF-SMS-015: Provider quality scores for routing engine consumption.</summary>
public sealed class ProviderQualityScore
{
    public string  ProviderType          { get; set; } = string.Empty;
    public Guid?   ProviderConfigId      { get; set; }
    public string? CountryCode           { get; set; }
    public decimal QualityScore          { get; set; }
    public decimal? CostEfficiencyScore  { get; set; }
    public decimal? AverageLatencyMs     { get; set; }
    /// <summary>Delivery success rate (0-1) from the underlying snapshot. Zero when no snapshot available.</summary>
    public decimal DeliverySuccessRate   { get; set; }
    public int     TotalAttempts         { get; set; }
    public bool    HasSufficientData     { get; set; }
    public DateTime? CalculatedAt        { get; set; }
}

/// <summary>
/// LS-NOTIF-SMS-015: Provider quality calculation service.
/// Reads only local Notification Service data. Never calls external providers.
/// Never stores phone numbers, credentials, or raw provider payloads.
/// </summary>
public interface ISmsProviderQualityService
{
    /// <summary>
    /// Calculate and persist quality snapshots for all providers within [windowStart, windowEnd].
    /// Idempotent — recalculating the same window overwrites existing snapshots.
    /// </summary>
    Task CalculateSnapshotsAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct);

    /// <summary>
    /// Get the latest quality score for a specific provider/tenant/country combination.
    /// Returns a score with HasSufficientData=false when telemetry is insufficient.
    /// </summary>
    Task<ProviderQualityScore> GetLatestScoreAsync(
        string  providerType,
        Guid?   tenantId,
        Guid?   providerConfigId,
        string? countryCode,
        CancellationToken ct);

    /// <summary>Get the latest quality score per provider (for routing engine use).</summary>
    Task<IReadOnlyList<ProviderQualityScore>> GetLatestScoresAsync(
        Guid?   tenantId,
        string? countryCode,
        CancellationToken ct);

    /// <summary>Query historical snapshots (for analytics APIs).</summary>
    Task<IReadOnlyList<SmsProviderQualitySnapshot>> QuerySnapshotsAsync(
        SmsQualitySnapshotQuery query,
        CancellationToken ct);
}
