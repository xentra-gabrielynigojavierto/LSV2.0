namespace BuildingBlocks.Context;

public interface ICurrentRequestContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TenantCode { get; }
    string? Email { get; }
    /// <summary>
    /// Full display name from the "name" JWT claim (e.g. "John Smith").
    /// Populated by JwtTokenService when the token is issued.
    /// Null for service-token callers or tokens issued before the claim was added.
    /// </summary>
    string? Name { get; }
    Guid? OrgId { get; }
    string? OrgType { get; }

    /// <summary>
    /// Phase B: canonical OrganizationType catalog ID from the org_type_id JWT claim.
    /// Null when the token was issued before org_type_id was added, or when the
    /// organization has not yet been assigned an OrganizationType.
    /// Prefer this over OrgType (string) in new code.
    /// </summary>
    Guid? OrgTypeId { get; }
    string? ProviderMode { get; }
    bool IsSellMode { get; }
    bool IsManageMode { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> ProductRoles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsPlatformAdmin { get; }
}
