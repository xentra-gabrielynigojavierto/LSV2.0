namespace Tenant.Application.Metrics;

/// <summary>
/// TENANT-B08 — In-process runtime metrics singleton for the Tenant service.
/// TENANT-STABILIZATION — Added identity proxy call counters for observability
///   during the B13 observation window. Tracks all operations proxied to Identity
///   via IIdentityCompatAdapter and IIdentityProvisioningAdapter.
///
/// All counters use Interlocked for thread-safety. Counters are process-lifetime
/// only and reset on service restart. Use GET /api/v1/admin/runtime-metrics to read.
/// </summary>
public sealed class TenantRuntimeMetrics
{
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;

    // ── Public branding reads ─────────────────────────────────────────────────
    private long _brandingPublicReadsAttempted;
    private long _brandingPublicReadsSucceeded;
    private long _brandingPublicReadsFailed;
    private long _brandingCacheHits;
    private long _brandingCacheMisses;

    public long BrandingPublicReadsAttempted => Interlocked.Read(ref _brandingPublicReadsAttempted);
    public long BrandingPublicReadsSucceeded => Interlocked.Read(ref _brandingPublicReadsSucceeded);
    public long BrandingPublicReadsFailed    => Interlocked.Read(ref _brandingPublicReadsFailed);
    public long BrandingCacheHits            => Interlocked.Read(ref _brandingCacheHits);
    public long BrandingCacheMisses          => Interlocked.Read(ref _brandingCacheMisses);

    public void IncrementBrandingAttempted()  => Interlocked.Increment(ref _brandingPublicReadsAttempted);
    public void IncrementBrandingSucceeded()  => Interlocked.Increment(ref _brandingPublicReadsSucceeded);
    public void IncrementBrandingFailed()     => Interlocked.Increment(ref _brandingPublicReadsFailed);
    public void IncrementBrandingCacheHit()   => Interlocked.Increment(ref _brandingCacheHits);
    public void IncrementBrandingCacheMiss()  => Interlocked.Increment(ref _brandingCacheMisses);

    // ── Resolution reads ──────────────────────────────────────────────────────
    private long _resolutionReadsAttempted;
    private long _resolutionReadsSucceeded;
    private long _resolutionReadsFailed;
    private long _resolutionCacheHits;
    private long _resolutionCacheMisses;

    public long ResolutionReadsAttempted => Interlocked.Read(ref _resolutionReadsAttempted);
    public long ResolutionReadsSucceeded => Interlocked.Read(ref _resolutionReadsSucceeded);
    public long ResolutionReadsFailed    => Interlocked.Read(ref _resolutionReadsFailed);
    public long ResolutionCacheHits      => Interlocked.Read(ref _resolutionCacheHits);
    public long ResolutionCacheMisses    => Interlocked.Read(ref _resolutionCacheMisses);

    public void IncrementResolutionAttempted()  => Interlocked.Increment(ref _resolutionReadsAttempted);
    public void IncrementResolutionSucceeded()  => Interlocked.Increment(ref _resolutionReadsSucceeded);
    public void IncrementResolutionFailed()     => Interlocked.Increment(ref _resolutionReadsFailed);
    public void IncrementResolutionCacheHit()   => Interlocked.Increment(ref _resolutionCacheHits);
    public void IncrementResolutionCacheMiss()  => Interlocked.Increment(ref _resolutionCacheMisses);

    // ── Tenant sync (dual-write) ──────────────────────────────────────────────
    private long _syncAttemptsReceived;
    private long _syncSucceeded;
    private long _syncFailed;

    public long SyncAttemptsReceived => Interlocked.Read(ref _syncAttemptsReceived);
    public long SyncSucceeded        => Interlocked.Read(ref _syncSucceeded);
    public long SyncFailed           => Interlocked.Read(ref _syncFailed);

    public void IncrementSyncAttempted() => Interlocked.Increment(ref _syncAttemptsReceived);
    public void IncrementSyncSucceeded() => Interlocked.Increment(ref _syncSucceeded);
    public void IncrementSyncFailed()    => Interlocked.Increment(ref _syncFailed);

    // ── Identity proxy calls (TENANT-STABILIZATION) ───────────────────────────
    // Tracks all calls that the Tenant service proxies to Identity admin endpoints.
    // These must remain non-zero only if Identity still owns the operation.
    // Target for B13 gate: only proxy counters should be non-zero (no direct CC→Identity calls).

    private long _identityProxySessionSettingsOk;
    private long _identityProxySessionSettingsFail;
    private long _identityProxyRetryProvisioningOk;
    private long _identityProxyRetryProvisioningFail;
    private long _identityProxyRetryVerificationOk;
    private long _identityProxyRetryVerificationFail;

    public long IdentityProxySessionSettingsOk       => Interlocked.Read(ref _identityProxySessionSettingsOk);
    public long IdentityProxySessionSettingsFail     => Interlocked.Read(ref _identityProxySessionSettingsFail);
    public long IdentityProxyRetryProvisioningOk     => Interlocked.Read(ref _identityProxyRetryProvisioningOk);
    public long IdentityProxyRetryProvisioningFail   => Interlocked.Read(ref _identityProxyRetryProvisioningFail);
    public long IdentityProxyRetryVerificationOk     => Interlocked.Read(ref _identityProxyRetryVerificationOk);
    public long IdentityProxyRetryVerificationFail   => Interlocked.Read(ref _identityProxyRetryVerificationFail);

    public void IncrementIdentityProxySessionSettingsOk()     => Interlocked.Increment(ref _identityProxySessionSettingsOk);
    public void IncrementIdentityProxySessionSettingsFail()   => Interlocked.Increment(ref _identityProxySessionSettingsFail);
    public void IncrementIdentityProxyRetryProvisioningOk()   => Interlocked.Increment(ref _identityProxyRetryProvisioningOk);
    public void IncrementIdentityProxyRetryProvisioningFail() => Interlocked.Increment(ref _identityProxyRetryProvisioningFail);
    public void IncrementIdentityProxyRetryVerificationOk()   => Interlocked.Increment(ref _identityProxyRetryVerificationOk);
    public void IncrementIdentityProxyRetryVerificationFail() => Interlocked.Increment(ref _identityProxyRetryVerificationFail);

    // ── Snapshot ──────────────────────────────────────────────────────────────
    public MetricsSnapshot Snapshot() => new(
        StartedAtUtc:                    StartedAtUtc,
        UptimeSeconds:                   (long)(DateTimeOffset.UtcNow - StartedAtUtc).TotalSeconds,
        BrandingAttempted:               BrandingPublicReadsAttempted,
        BrandingSucceeded:               BrandingPublicReadsSucceeded,
        BrandingFailed:                  BrandingPublicReadsFailed,
        BrandingCacheHits:               BrandingCacheHits,
        BrandingCacheMisses:             BrandingCacheMisses,
        ResolutionAttempted:             ResolutionReadsAttempted,
        ResolutionSucceeded:             ResolutionReadsSucceeded,
        ResolutionFailed:                ResolutionReadsFailed,
        ResolutionCacheHits:             ResolutionCacheHits,
        ResolutionCacheMisses:           ResolutionCacheMisses,
        SyncAttemptsReceived:            SyncAttemptsReceived,
        SyncSucceeded:                   SyncSucceeded,
        SyncFailed:                      SyncFailed,
        IdentityProxySessionSettingsOk:     IdentityProxySessionSettingsOk,
        IdentityProxySessionSettingsFail:   IdentityProxySessionSettingsFail,
        IdentityProxyRetryProvisioningOk:   IdentityProxyRetryProvisioningOk,
        IdentityProxyRetryProvisioningFail: IdentityProxyRetryProvisioningFail,
        IdentityProxyRetryVerificationOk:   IdentityProxyRetryVerificationOk,
        IdentityProxyRetryVerificationFail: IdentityProxyRetryVerificationFail);
}

public record MetricsSnapshot(
    DateTimeOffset StartedAtUtc,
    long           UptimeSeconds,
    long           BrandingAttempted,
    long           BrandingSucceeded,
    long           BrandingFailed,
    long           BrandingCacheHits,
    long           BrandingCacheMisses,
    long           ResolutionAttempted,
    long           ResolutionSucceeded,
    long           ResolutionFailed,
    long           ResolutionCacheHits,
    long           ResolutionCacheMisses,
    long           SyncAttemptsReceived,
    long           SyncSucceeded,
    long           SyncFailed,
    // Identity proxy counters — TENANT-STABILIZATION
    long           IdentityProxySessionSettingsOk,
    long           IdentityProxySessionSettingsFail,
    long           IdentityProxyRetryProvisioningOk,
    long           IdentityProxyRetryProvisioningFail,
    long           IdentityProxyRetryVerificationOk,
    long           IdentityProxyRetryVerificationFail);
