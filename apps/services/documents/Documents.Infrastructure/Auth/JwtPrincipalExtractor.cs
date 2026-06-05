using Documents.Domain.ValueObjects;
using System.Security.Claims;

namespace Documents.Infrastructure.Auth;

public static class JwtPrincipalExtractor
{
    /// <summary>
    /// Extracts a <see cref="Principal"/> from ASP.NET Core's ClaimsPrincipal.
    /// Supports both standard and custom claim naming conventions used by the platform.
    /// </summary>
    public static Principal Extract(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
               ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("userId")
               ?? throw new UnauthorizedAccessException("JWT missing 'sub' claim");

        var tenantIdRaw = user.FindFirstValue("tenantId")
                       ?? user.FindFirstValue("tenant_id")
                       ?? throw new UnauthorizedAccessException("JWT missing 'tenantId' claim");

        if (!Guid.TryParse(sub, out var userId))
            userId = Guid.Empty;   // non-UUID subject — use empty (non-admin, non-scoped)

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
            throw new UnauthorizedAccessException("JWT 'tenantId' claim is not a valid UUID");

        var email = user.FindFirstValue("email")
                 ?? user.FindFirstValue(ClaimTypes.Email);

        // Roles may be a single claim or multiple claims
        var roles = user.FindAll("roles")
            .Concat(user.FindAll("role"))
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        return new Principal
        {
            UserId   = userId,
            TenantId = tenantId,
            Email    = email,
            Roles    = roles,
        };
    }
}
