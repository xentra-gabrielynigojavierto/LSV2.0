using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Authorization.Filters;

public sealed class RequireSellModeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var providerMode = user.FindFirst("provider_mode")?.Value;
        var isSellMode = string.IsNullOrEmpty(providerMode) ||
                         string.Equals(providerMode, "sell", StringComparison.OrdinalIgnoreCase);

        if (!isSellMode)
        {
            var userId = user.FindFirst("sub")?.Value;
            var tenantId = user.FindFirst("tenant_id")?.Value;
            var path = httpContext.Request.Path.Value;
            var method = httpContext.Request.Method;

            var logger = httpContext.RequestServices.GetService(typeof(ILogger<RequireSellModeFilter>)) as ILogger;
            logger?.LogWarning(
                "ProviderModeBlock: user={UserId} tenant={TenantId} method={Method} endpoint={Path} providerMode={ProviderMode} reason=ManageModeRestricted",
                userId, tenantId, method, path, providerMode);

            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "PROVIDER_MODE_RESTRICTED",
                        message = "This operation is not available in manage mode. Your organization is configured for internal lien management only."
                    }
                },
                statusCode: 403);
        }

        return await next(context);
    }
}
