namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-014: SMS routing policy entity.
/// Defines how outbound SMS messages should be routed for a tenant (or platform-wide).
///
/// TenantId = null → global/platform policy (applies as fallback when no tenant-specific policy matches).
/// TenantId set    → tenant-specific policy (takes precedence over global).
///
/// Priority: lower value = evaluated first within the same scope.
/// RoutingMode: priority | cost_optimized | health_optimized | hybrid | regional
///
/// No credentials stored — policies reference provider types by name only.
/// </summary>
public class SmsRoutingPolicy
{
    public Guid    Id          { get; set; } = Guid.NewGuid();

    /// <summary>null = global/platform policy</summary>
    public Guid?   TenantId    { get; set; }

    public string  Name        { get; set; } = string.Empty;
    public bool    Enabled     { get; set; } = true;

    /// <summary>Optional region constraint (e.g., "us-east"). null = all regions.</summary>
    public string? Region      { get; set; }

    /// <summary>Optional ISO country code constraint (e.g., "US"). null = all countries.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Routing mode: priority | cost_optimized | health_optimized | hybrid | regional</summary>
    public string  RoutingMode { get; set; } = "priority";

    /// <summary>JSON string array of preferred provider types in preferred order.</summary>
    public string? PreferredProvidersJson { get; set; }

    /// <summary>JSON string array of excluded provider types.</summary>
    public string? ExcludedProvidersJson  { get; set; }

    /// <summary>Cost cap: if set, providers exceeding this estimated cost are excluded.</summary>
    public decimal? MaxEstimatedCostPerMessage { get; set; }

    /// <summary>If true, skip providers with health_status = "down".</summary>
    public bool    RequireHealthyProvider { get; set; } = false;

    /// <summary>If true, allow platform fallback when all tenant routes are exhausted.</summary>
    public bool    FallbackToPlatform    { get; set; } = true;

    /// <summary>Lower value = higher precedence when multiple policies match.</summary>
    public int     Priority              { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string?  CreatedBy { get; set; }
    public string?  UpdatedBy { get; set; }
}
