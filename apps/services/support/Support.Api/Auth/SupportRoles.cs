namespace Support.Api.Auth;

public static class SupportRoles
{
    public const string PlatformAdmin  = "PlatformAdmin";
    public const string SupportAdmin   = "SupportAdmin";
    public const string SupportManager = "SupportManager";
    public const string SupportAgent   = "SupportAgent";
    public const string TenantAdmin    = "TenantAdmin";
    public const string TenantUser     = "TenantUser";

    /// <summary>
    /// StandardUser is the Identity role name for regular tenant users created before
    /// the TenantUser role was seeded (migration 20260426000001). Included in All
    /// so that users provisioned under the old schema can access support endpoints
    /// without re-provisioning.
    /// </summary>
    public const string StandardUser = "StandardUser";

    /// <summary>
    /// External customer role — intentionally excluded from All, InternalStaff, and
    /// Managers so that existing SupportRead/Write/Manage/Internal policies are
    /// completely unaffected. Only the CustomerAccess policy grants this role access.
    /// </summary>
    public const string ExternalCustomer = "ExternalCustomer";

    public static readonly string[] All =
    {
        PlatformAdmin, SupportAdmin, SupportManager, SupportAgent,
        TenantAdmin, TenantUser, StandardUser,
    };

    public static readonly string[] InternalStaff =
    {
        PlatformAdmin, SupportAdmin, SupportManager, SupportAgent
    };

    public static readonly string[] Managers =
    {
        PlatformAdmin, SupportAdmin, SupportManager
    };
}

public static class SupportPolicies
{
    public const string SupportRead     = "SupportRead";
    public const string SupportWrite    = "SupportWrite";
    public const string SupportManage   = "SupportManage";
    public const string SupportInternal = "SupportInternal";

    /// <summary>
    /// Customer access policy — requires authenticated user with ExternalCustomer role.
    /// Grants access only to customer-scoped endpoints that enforce tenantId +
    /// externalCustomerId + CustomerVisible constraints at the service layer.
    ///
    /// JWT claim contract:
    ///   role: ExternalCustomer
    ///   tenant_id: &lt;string&gt;          (resolved by TenantResolutionMiddleware)
    ///   external_customer_id: &lt;uuid&gt;  (read directly in each endpoint handler)
    ///   email: &lt;string&gt;             (optional — used as comment author email)
    ///   name: &lt;string&gt;              (optional — used as comment author name)
    /// </summary>
    public const string CustomerAccess  = "CustomerAccess";
}
