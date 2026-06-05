using System.Security.Claims;

namespace BuildingBlocks.Authorization;

public interface IPolicyEvaluationService
{
    Task<PolicyEvaluationResult> EvaluateAsync(
        ClaimsPrincipal user,
        string permissionCode,
        Dictionary<string, object?>? resourceContext = null,
        Microsoft.AspNetCore.Http.HttpContext? httpContext = null,
        CancellationToken ct = default);
}
