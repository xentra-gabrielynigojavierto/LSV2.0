namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-005: Result of resolving a referral view token.
///
/// RouteType values:
///   "pending"  — provider has no Identity org link; route to public accept page
///   "active"   — provider is linked to a tenant; route through platform login
///   "invalid"  — token is malformed, expired, or tampered with
///   "notfound" — referral no longer exists
/// </summary>
public class ReferralViewTokenRouteResponse
{
    public string  RouteType  { get; init; } = "invalid";
    public Guid?   ReferralId { get; init; }
    /// <summary>Tenant code for active-tenant providers (used to build the login redirect URL).</summary>
    public string? TenantCode { get; init; }
}
