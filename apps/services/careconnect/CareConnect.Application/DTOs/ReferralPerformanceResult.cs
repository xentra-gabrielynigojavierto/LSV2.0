// LSCC-01-005: Referral Performance Metrics — data transfer objects.
namespace CareConnect.Application.DTOs;

/// <summary>
/// Top-level response returned by GET /api/admin/performance.
///
/// Time-window anchor: referral.CreatedAtUtc >= since (for all cohort metrics).
/// Aging distribution covers ALL currently-New referrals regardless of creation window.
/// </summary>
public sealed record ReferralPerformanceResult
{
    /// <summary>UTC start of the selected performance window.</summary>
    public required DateTime WindowFrom   { get; init; }

    /// <summary>UTC end of the window (query time).</summary>
    public required DateTime WindowTo     { get; init; }

    public required PerformanceSummary   Summary  { get; init; }
    public required AgingDistribution    Aging    { get; init; }
    public required IReadOnlyList<ProviderPerformanceRow> Providers { get; init; }
}

/// <summary>
/// Aggregate summary over the selected window.
///
/// AcceptanceRate:
///   - Accepted / Total, as a decimal [0,1]
///   - Returns 0 when Total is 0 (divide-by-zero guard)
///
/// AvgTimeToAcceptHours:
///   - Average (AcceptedAtUtc - CreatedAtUtc).TotalHours for accepted referrals
///   - AcceptedAtUtc is derived from the earliest ReferralStatusHistory entry where
///     NewStatus == "Accepted" for that referral
///   - Records with AcceptedAtUtc < CreatedAtUtc (corrupt data) are excluded
///   - Returns null when no valid accepted referrals exist
/// </summary>
public sealed record PerformanceSummary
{
    public required int     TotalReferrals        { get; init; }
    public required int     AcceptedReferrals     { get; init; }
    public required double  AcceptanceRate        { get; init; }  // [0,1]
    public required double? AvgTimeToAcceptHours  { get; init; }  // null when no accepted
    public required int     CurrentNewReferrals   { get; init; }  // count at query time
}

/// <summary>
/// Distribution of currently-New referrals by how long they have been waiting.
///
/// Buckets:
///   Lt1h    = &lt;  1 hour
///   H1To24  = 1–24 hours   (≥ 1h, &lt; 24h)
///   D1To3   = 1–3 days     (≥ 24h, &lt; 72h)
///   Gt3d    = 3+ days      (≥ 72h)
///
/// Age is computed as: NowUtc - referral.CreatedAtUtc.
/// Only referrals currently in Status=New are included.
/// </summary>
public sealed record AgingDistribution
{
    public required int Lt1h   { get; init; }
    public required int H1To24 { get; init; }
    public required int D1To3  { get; init; }
    public required int Gt3d   { get; init; }

    public int Total => Lt1h + H1To24 + D1To3 + Gt3d;
}

/// <summary>
/// Per-provider performance row.
///
/// AvgTimeToAcceptHours is null when AcceptedReferrals is 0.
/// All providers with at least one referral in the window appear — even if zero accepted.
/// </summary>
public sealed record ProviderPerformanceRow
{
    public required Guid    ProviderId             { get; init; }
    public required string  ProviderName           { get; init; }
    public required int     TotalReferrals         { get; init; }
    public required int     AcceptedReferrals      { get; init; }
    public required double  AcceptanceRate         { get; init; }  // [0,1]
    public required double? AvgTimeToAcceptHours   { get; init; }  // null when 0 accepted
}

/// <summary>
/// Internal flat record used by the service to pass DB-loaded data to the calculator.
/// Not exposed via the API.
/// </summary>
public sealed record RawReferralRecord(
    Guid     Id,
    DateTime CreatedAtUtc,
    string   Status,
    Guid     ProviderId,
    string   ProviderName,
    /// <summary>
    /// Derived from the earliest ReferralStatusHistory entry where NewStatus==Accepted.
    /// Null if the referral has never been accepted or if derivation fails.
    /// </summary>
    DateTime? AcceptedAtUtc);
