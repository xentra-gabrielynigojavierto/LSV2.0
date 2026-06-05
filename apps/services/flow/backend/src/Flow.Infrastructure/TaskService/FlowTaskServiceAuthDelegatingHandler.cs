using System.Net.Http.Headers;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.TaskService;

/// <summary>
/// TASK-FLOW-01 — outbound auth delegating handler for Flow → Task service calls.
///
/// <para>
/// All Task service endpoints used by Flow require a valid user JWT
/// (<c>AuthenticatedUser</c> policy), not a service token. This handler
/// therefore forwards the caller's bearer token from the ambient HTTP context.
/// </para>
///
/// <para>
/// Fallback: if no HTTP context is available (background service) or the
/// context carries no Authorization header, the handler falls back to minting
/// a short-lived HS256 service token — applicable only for Task service
/// endpoints that accept <c>InternalService</c> policy. All Flow write paths
/// run in HTTP-request scope, so the fallback is purely defensive.
/// </para>
/// </summary>
public sealed class FlowTaskServiceAuthDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor                       _httpContext;
    private readonly IServiceTokenIssuer                        _issuer;
    private readonly ILogger<FlowTaskServiceAuthDelegatingHandler> _logger;

    public FlowTaskServiceAuthDelegatingHandler(
        IHttpContextAccessor                          httpContext,
        IServiceTokenIssuer                           issuer,
        ILogger<FlowTaskServiceAuthDelegatingHandler> logger)
    {
        _httpContext = httpContext;
        _issuer      = issuer;
        _logger      = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
    {
        // Prefer forwarding the caller's bearer token verbatim so the
        // Task service authenticates the actual user (AuthenticatedUser policy).
        var incomingAuth = _httpContext.HttpContext?
            .Request.Headers["Authorization"]
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(incomingAuth) &&
            incomingAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", incomingAuth["Bearer ".Length..]);
            return await base.SendAsync(request, cancellationToken);
        }

        // Fallback: mint a service token (for background / internal paths).
        if (_issuer.IsConfigured &&
            request.Headers.TryGetValues("X-Tenant-Id", out var vals))
        {
            var tenantId = vals.FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantId))
            {
                try
                {
                    var token = _issuer.IssueToken(tenantId);
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "FlowTaskServiceAuthDelegatingHandler: could not mint service token for tenant {TenantId}.",
                        tenantId);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
