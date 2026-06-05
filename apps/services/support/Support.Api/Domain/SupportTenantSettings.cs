namespace Support.Api.Domain;

/// <summary>
/// Per-tenant Support configuration stored in `support_tenant_settings`.
///
/// There is at most one row per tenant. If no row exists for a tenant, the
/// effective mode is InternalOnly with CustomerPortalEnabled=false.
///
/// TenantId is the PK — it acts as the isolation boundary.
/// No FK to the Identity tenant table; cross-service FKs are avoided per platform convention.
/// </summary>
public class SupportTenantSettings
{
    /// <summary>
    /// The tenant identifier — matches the tenant_id JWT claim.
    /// Acts as both the PK and the isolation key.
    /// </summary>
    public string TenantId { get; set; } = default!;

    /// <summary>
    /// Determines which Support mode is active for this tenant.
    /// Default: InternalOnly.
    /// </summary>
    public SupportTenantMode SupportMode { get; set; } = SupportTenantMode.InternalOnly;

    /// <summary>
    /// Whether the customer-facing Support portal is enabled.
    /// Effective customer access requires BOTH SupportMode=TenantCustomerSupport AND CustomerPortalEnabled=true.
    /// Default: false.
    /// </summary>
    public bool CustomerPortalEnabled { get; set; } = false;

    /// <summary>UTC timestamp when this settings row was first created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update. Null if never updated after creation.</summary>
    public DateTime? UpdatedAt { get; set; }
}
