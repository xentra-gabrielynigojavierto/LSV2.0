using System.Security.Claims;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Context;

public class CurrentRequestContext : ICurrentRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            // Normal user tokens: sub is the user's GUID.
            if (Guid.TryParse(User?.FindFirstValue("sub"), out var uid))
                return uid;

            // Service-token callers (sub = "service:*"): the acting user's GUID is
            // carried in the signed "actor" claim (format: "user:<guid>") minted by
            // ServiceTokenIssuer. The unsigned X-User-Id transport header is ignored
            // because it is not authenticated and can be forged by any caller.
            var actor = User?.FindFirstValue(ServiceTokenAuthenticationDefaults.ActorClaim);
            if (actor is not null &&
                actor.StartsWith("user:", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(actor.AsSpan(5), out var actorUid))
                return actorUid;

            return null;
        }
    }

    public Guid? TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var tid) ? tid : null;

    public string? TenantCode => User?.FindFirstValue("tenant_code");

    public string? Email => User?.FindFirstValue("email");

    public string? Name => User?.FindFirstValue("name");

    public Guid? OrgId =>
        Guid.TryParse(User?.FindFirstValue("org_id"), out var oid) ? oid : null;

    public string? OrgType => User?.FindFirstValue("org_type");

    /// <summary>
    /// Phase B: canonical OrganizationType catalog ID emitted by JwtTokenService
    /// as the "org_type_id" claim. Null when the token predates Phase B or the org
    /// has no OrganizationType assigned yet. Prefer over OrgType in new code.
    /// </summary>
    public Guid? OrgTypeId =>
        Guid.TryParse(User?.FindFirstValue("org_type_id"), out var otid) ? otid : null;

    public string? ProviderMode => User?.FindFirstValue("provider_mode");

    public bool IsSellMode =>
        string.IsNullOrEmpty(ProviderMode) ||
        string.Equals(ProviderMode, "sell", StringComparison.OrdinalIgnoreCase);

    public bool IsManageMode =>
        string.Equals(ProviderMode, "manage", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public IReadOnlyCollection<string> ProductRoles =>
        User?.FindAll("product_roles").Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permissions").Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public bool IsPlatformAdmin =>
        Roles.Contains(Authorization.Roles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);
}
