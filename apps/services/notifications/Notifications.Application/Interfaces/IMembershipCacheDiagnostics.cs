namespace Notifications.Application.Interfaces;

/// <summary>
/// Operator-facing snapshot of role/org membership cache activity inside the
/// notifications service. Surfaced via <c>GET /internal/membership-cache/stats</c>
/// so ops can verify identity → notifications wiring is healthy without
/// having to grep the logs.
/// </summary>
public sealed record MembershipCacheStatsSnapshot
{
    /// <summary>True when notifications has an Identity base URL configured;
    /// false means it falls back to the in-memory provider (test/dev only).</summary>
    public bool   IdentityConfigured       { get; init; }

    /// <summary>Configured cache TTL (seconds). 0 disables caching entirely.</summary>
    public int    CacheTtlSeconds          { get; init; }

    /// <summary>Number of role/org lookups served from the in-process cache
    /// since process start.</summary>
    public long   Hits                     { get; init; }

    /// <summary>Number of role/org lookups that fell through to identity
    /// (cache miss, expired, or first lookup of a key) since process start.</summary>
    public long   Misses                   { get; init; }

    /// <summary>Number of times a tenant version was bumped via
    /// <c>POST /internal/membership-cache/invalidate</c>.</summary>
    public long   Invalidations            { get; init; }

    /// <summary>UTC timestamp of the most recent successful invalidation
    /// (null until the first one fires).</summary>
    public DateTime? LastInvalidationUtc   { get; init; }

    /// <summary>Hit ratio in [0,1]; null when there have been no lookups yet.</summary>
    public double? HitRatio
        => (Hits + Misses) == 0 ? null : (double)Hits / (Hits + Misses);
}

/// <summary>
/// Snapshot accessor for membership cache counters. Implemented by
/// <c>HttpRoleMembershipProvider</c> in production; a no-op variant is
/// registered when notifications falls back to the in-memory provider so
/// the diagnostics endpoint stays functional in dev/test.
/// </summary>
public interface IMembershipCacheDiagnostics
{
    MembershipCacheStatsSnapshot GetSnapshot();
}
