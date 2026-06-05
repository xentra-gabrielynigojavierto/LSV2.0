using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Diagnostics stub used when notifications is running without an Identity
/// base URL configured (in-memory membership provider). The endpoint still
/// responds so operators can confirm the wiring is intentionally disabled
/// rather than mis-configured.
/// </summary>
public sealed class NoOpMembershipCacheDiagnostics : IMembershipCacheDiagnostics
{
    public MembershipCacheStatsSnapshot GetSnapshot() => new()
    {
        IdentityConfigured  = false,
        CacheTtlSeconds     = 0,
        Hits                = 0,
        Misses              = 0,
        Invalidations       = 0,
        LastInvalidationUtc = null,
    };
}
