using System.Security.Claims;

namespace BuildingBlocks.Authorization;

public interface IAttributeProvider
{
    Task<Dictionary<string, object?>> GetUserAttributesAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<Dictionary<string, object?>> GetResourceAttributesAsync(Dictionary<string, object?>? resourceContext, CancellationToken ct = default);
    Task<Dictionary<string, object?>> GetRequestContextAsync(Microsoft.AspNetCore.Http.HttpContext httpContext, CancellationToken ct = default);
}
