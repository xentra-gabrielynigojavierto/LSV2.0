namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — header-driven authentication scheme used only by
/// the integration host. The TestAuth handler builds a real ClaimsPrincipal
/// from <c>X-Test-*</c> headers so the production
/// <c>ClaimsTenantProvider</c>, <c>CallerContextAccessor</c>, capability
/// policies and tenant query filter all run unmodified — i.e. the policies
/// under test are the production policies.
/// </summary>
public static class TestAuthDefaults
{
    public const string Scheme = "TestAuth";

    // Header names — kept short and prefixed so they cannot collide with any
    // production header. Anything missing is treated as "not present".
    public const string SubHeader          = "X-Test-Sub";
    public const string TenantHeader       = "X-Test-Tenant";
    public const string RoleHeader         = "X-Test-Role";
    public const string PermissionsHeader  = "X-Test-Permissions";   // comma-separated
    public const string ProductRolesHeader = "X-Test-ProductRoles";  // comma-separated
    public const string ActorHeader        = "X-Test-Actor";         // service-on-behalf-of-user
    public const string AudHeader          = "X-Test-Aud";           // optional aud claim
}
