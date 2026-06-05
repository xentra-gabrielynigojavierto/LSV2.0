using System.Net.Http.Headers;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-B04 / TASK-010 — outbound auth delegating handler for calls to the canonical Task service.
/// Mirrors the NotificationsAuthDelegatingHandler pattern from BuildingBlocks.
/// Reads the X-Tenant-Id header set by the caller, mints a short-lived HS256 service JWT,
/// and injects Authorization: Bearer. Falls back silently when the signing key is not configured.
/// </summary>
public sealed class TaskServiceAuthDelegatingHandler : DelegatingHandler
{
    private readonly IServiceTokenIssuer _issuer;
    private readonly ILogger<TaskServiceAuthDelegatingHandler> _logger;

    public TaskServiceAuthDelegatingHandler(
        IServiceTokenIssuer issuer,
        ILogger<TaskServiceAuthDelegatingHandler> logger)
    {
        _issuer = issuer;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
    {
        if (_issuer.IsConfigured &&
            request.Headers.TryGetValues("X-Tenant-Id", out var vals))
        {
            var tenantId = vals.FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantId))
            {
                try
                {
                    // Forward the acting user id (X-User-Id) into the signed actor claim
                    // so that CurrentRequestContext.UserId resolves correctly on write endpoints.
                    request.Headers.TryGetValues("X-User-Id", out var userIdVals);
                    var actorUserId = userIdVals?.FirstOrDefault();

                    var token = _issuer.IssueToken(tenantId, actorUserId);
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "TaskServiceAuthDelegatingHandler: could not mint service token for tenant {TenantId}.",
                        tenantId);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
