namespace Tenant.Application.Configuration;

/// <summary>
/// Phased read-source feature flags for the Tenant service.
///
/// TENANT-B09: All read-source defaults are now <c>Tenant</c>.
/// Identity mode is retained for rollback only — set any flag to <c>Identity</c>
/// to revert a specific path. Set to <c>HybridFallback</c> for a soft transition.
///
/// Configure via appsettings.json Features section or environment variables
/// (Features__TenantReadSource=HybridFallback, etc.).
/// </summary>
public class TenantFeatures
{
    public const string SectionName = "Features";

    /// <summary>
    /// Overall default read-source for all tenant-related runtime reads.
    /// Per-consumer overrides below take precedence when explicitly set.
    ///
    /// TENANT-B09: Default changed from <c>Identity</c> to <c>Tenant</c>.
    /// Rollback: set to <c>Identity</c> or <c>HybridFallback</c>.
    /// </summary>
    public TenantReadSource TenantReadSource { get; set; } = TenantReadSource.Tenant;

    /// <summary>
    /// Read-source for public branding bootstrap (login page, anonymous branding).
    /// When set, overrides TenantReadSource for branding lookups.
    ///
    /// TENANT-B09: Default changed from <c>Identity</c> to <c>Tenant</c>.
    /// </summary>
    public TenantReadSource TenantBrandingReadSource { get; set; } = TenantReadSource.Tenant;

    /// <summary>
    /// Read-source for tenant resolution by host/subdomain/code.
    /// When set, overrides TenantReadSource for resolution lookups.
    ///
    /// TENANT-B09: Default changed from <c>Identity</c> to <c>Tenant</c>.
    /// </summary>
    public TenantReadSource TenantResolutionReadSource { get; set; } = TenantReadSource.Tenant;

    /// <summary>
    /// Activates dual-write via ITenantSyncAdapter.
    ///
    /// TENANT-B09: Default changed to <c>true</c> — dual-write is now expected to
    /// be active in all environments where the Tenant service is deployed.
    /// Disable only for rollback or in isolated test environments.
    /// </summary>
    public bool TenantDualWriteEnabled { get; set; } = true;

    /// <summary>
    /// TENANT-B07 — When true, a Tenant sync failure from Identity aborts the originating
    /// Identity operation (returns 502 to the caller). When false (default), sync failures
    /// are logged and the Identity operation continues normally.
    /// Only enable in controlled environments where Tenant service health is confirmed.
    /// </summary>
    public bool TenantDualWriteStrictMode { get; set; } = false;

    // ── TENANT-B08: Caching / Performance Hardening ───────────────────────────

    /// <summary>
    /// TENANT-B08 — Enable in-process IMemoryCache on public branding and
    /// resolution read paths. Safe to disable for debugging or rollback.
    /// Default: true.
    /// </summary>
    public bool TenantReadCachingEnabled { get; set; } = true;

    /// <summary>
    /// TENANT-B08 — Time-to-live in seconds for cached branding and resolution
    /// results. Applies to all cached public read paths.
    /// Default: 60 seconds.
    /// </summary>
    public int TenantReadCacheTtlSeconds { get; set; } = 60;
}

/// <summary>
/// Controls which service is consulted for tenant data reads.
/// </summary>
public enum TenantReadSource
{
    /// <summary>Legacy Identity service path. Safe default — no behavior change.</summary>
    Identity,

    /// <summary>Read exclusively from the Tenant service.</summary>
    Tenant,

    /// <summary>
    /// Try the Tenant service first; fall back to Identity on failure,
    /// 404, or incomplete required fields.
    /// </summary>
    HybridFallback,
}
