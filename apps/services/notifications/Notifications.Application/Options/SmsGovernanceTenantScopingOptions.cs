namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-023: Configuration for per-tenant governance rule pack scoping.
/// Safe defaults ensure existing global behaviour is preserved when not explicitly configured.
/// </summary>
public class SmsGovernanceTenantScopingOptions
{
    public const string SectionName = "SmsGovernanceTenantScoping";

    /// <summary>Master switch. When false, all resolution falls back to global behaviour.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Resolution mode applied when tenant has no explicit assignment.
    /// - global_only: only global packs (existing LS-017..022 behaviour)
    /// - tenant_inherited: global packs + tenant assigned packs merged
    /// - tenant_isolated: only tenant assigned packs (requires explicit assignment)
    /// - rollout_canary: rollout-canary assignments only
    /// </summary>
    public string ResolutionMode { get; set; } = ResolutionModes.TenantInherited;

    /// <summary>When true, resolution errors return global rules (fail open).</summary>
    public bool FailOpenOnResolutionError { get; set; } = true;

    /// <summary>Whether tenant overlay logic is applied during resolution.</summary>
    public bool EnableTenantOverlays { get; set; } = true;

    /// <summary>Whether rollout-created assignments are applied during resolution.</summary>
    public bool EnableRolloutAssignments { get; set; } = true;

    /// <summary>Maximum active assignments per tenant (enforced by assignment service).</summary>
    public int MaxAssignmentsPerTenant { get; set; } = 50;

    /// <summary>Maximum active overlays per tenant.</summary>
    public int MaxOverlaysPerTenant { get; set; } = 100;

    /// <summary>When true, cache resolved rules for a tenant during a single request scope.</summary>
    public bool CacheResolvedRules { get; set; } = false;

    /// <summary>Cache TTL in seconds (only relevant when CacheResolvedRules = true).</summary>
    public int ResolutionCacheSeconds { get; set; } = 60;

    public static class ResolutionModes
    {
        public const string GlobalOnly      = "global_only";
        public const string TenantInherited = "tenant_inherited";
        public const string TenantIsolated  = "tenant_isolated";
        public const string RolloutCanary   = "rollout_canary";
    }
}
