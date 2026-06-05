namespace Identity.Domain;

/// <summary>
/// PUM-B01: Unified user type classification.
/// Distinguishes platform-internal staff, tenant users, and
/// external/customer-facing users (future Commerce/Support access).
/// </summary>
public enum UserType
{
    /// <summary>Tenant user — the default for all users created via the tenant flow.</summary>
    TenantUser = 0,

    /// <summary>LegalSynq platform-internal staff (e.g. Control Center admins).</summary>
    PlatformInternal = 1,

    /// <summary>External customer user, reserved for future Commerce and Support portals.</summary>
    ExternalCustomer = 2,
}
