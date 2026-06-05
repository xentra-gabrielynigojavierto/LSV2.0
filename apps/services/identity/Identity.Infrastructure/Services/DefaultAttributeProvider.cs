using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.Infrastructure.Services;

public class DefaultAttributeProvider : IAttributeProvider
{
    public Task<Dictionary<string, object?>> GetUserAttributesAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var attrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var sub = user.FindFirst("sub")?.Value;
        if (sub != null) attrs["userId"] = sub;

        var tenantId = user.FindFirst("tenant_id")?.Value;
        if (tenantId != null) attrs["tenantId"] = tenantId;

        var orgId = user.FindFirst("org_id")?.Value;
        if (orgId != null) attrs["organizationId"] = orgId;

        // MapInboundClaims=false keeps JWT "role" claims as type "role" (short name),
        // not ClaimTypes.Role (long URI). Use the short name to find them.
        var roles = user.FindAll("role").Select(c => c.Value).ToList();
        if (roles.Count == 1) attrs["role"] = roles[0];
        else if (roles.Count > 1) attrs["role"] = roles;

        var region = user.FindFirst("region")?.Value;
        if (region != null) attrs["region"] = region;

        var department = user.FindFirst("department")?.Value;
        if (department != null) attrs["department"] = department;

        return Task.FromResult(attrs);
    }

    public Task<Dictionary<string, object?>> GetResourceAttributesAsync(
        Dictionary<string, object?>? resourceContext, CancellationToken ct = default)
    {
        return Task.FromResult(resourceContext ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
    }

    public Task<Dictionary<string, object?>> GetRequestContextAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var ctx = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["ip"] = httpContext.Connection.RemoteIpAddress?.ToString(),
        };

        return Task.FromResult(ctx);
    }
}
