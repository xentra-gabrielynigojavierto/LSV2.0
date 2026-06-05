using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>LS-NOTIF-SMS-016: Query for recipient reputation snapshots.</summary>
public sealed class SmsRecipientAnalyticsQuery
{
    public Guid?   TenantId    { get; set; }
    public string? Provider    { get; set; }
    public string? CountryCode { get; set; }
    public string? Region      { get; set; }
    /// <summary>low | medium | high | suppressed</summary>
    public string? RiskLevel   { get; set; }
    public DateTime? From      { get; set; }
    public DateTime? To        { get; set; }
    public int Limit           { get; set; } = 50;
    public int Offset          { get; set; } = 0;
}

/// <summary>LS-NOTIF-SMS-016: Query for suppression decisions.</summary>
public sealed class SmsSuppressionDecisionQuery
{
    public Guid?   TenantId     { get; set; }
    public string? DecisionType { get; set; }
    public string? ReasonCode   { get; set; }
    public string? Provider     { get; set; }
    public string? CountryCode  { get; set; }
    public DateTime? From       { get; set; }
    public DateTime? To         { get; set; }
    public int Limit            { get; set; } = 50;
    public int Offset           { get; set; } = 0;
}

/// <summary>
/// LS-NOTIF-SMS-016: Recipient intelligence aggregation and scoring service.
/// Uses only local Notification Service telemetry — no external enrichment APIs.
/// Never stores or returns raw phone numbers.
/// </summary>
public interface ISmsRecipientIntelligenceService
{
    /// <summary>
    /// Calculate and persist recipient reputation snapshots for attempts in [windowStart, windowEnd].
    /// Extracts recipient phone from Notification.RecipientJson, hashes it, aggregates telemetry.
    /// Idempotent — recalculating the same window overwrites snapshots for the same hash/tenant/provider.
    /// </summary>
    Task CalculateSnapshotsAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct);

    /// <summary>Get the latest reputation snapshot for a specific recipient hash and tenant.</summary>
    Task<SmsRecipientReputationSnapshot?> GetRecipientSnapshotAsync(
        string recipientHash,
        Guid?  tenantId,
        CancellationToken ct);

    /// <summary>Query recipient reputation snapshots for analytics views. Returns safe aggregate data only.</summary>
    Task<(IReadOnlyList<SmsRecipientReputationSnapshot> Items, int Total)> QueryRecipientAnalyticsAsync(
        SmsRecipientAnalyticsQuery query,
        CancellationToken ct);

    /// <summary>Query suppression decision log. Returns safe audit data only.</summary>
    Task<(IReadOnlyList<SmsSuppressionDecision> Items, int Total)> QuerySuppressionDecisionsAsync(
        SmsSuppressionDecisionQuery query,
        CancellationToken ct);

    /// <summary>Persist a suppression decision record (audit trail).</summary>
    Task PersistSuppressionDecisionAsync(
        SmsSuppressionDecision decision,
        CancellationToken ct);

    /// <summary>Get risk level distribution summary for admin dashboard.</summary>
    Task<Dictionary<string, long>> GetRiskDistributionAsync(
        Guid? tenantId,
        string? countryCode,
        CancellationToken ct);
}
