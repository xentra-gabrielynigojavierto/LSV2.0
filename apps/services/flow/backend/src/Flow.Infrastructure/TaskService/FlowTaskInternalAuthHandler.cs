using System.Net.Http.Headers;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.TaskService;

/// <summary>
/// TASK-FLOW-02 — outbound auth delegating handler for internal Flow → Task service calls.
///
/// <para>
/// Unlike <see cref="FlowTaskServiceAuthDelegatingHandler"/>, this handler ALWAYS mints
/// a service token (never forwards the user bearer token). It is intended exclusively
/// for the Task service's <c>InternalService</c>-policy endpoints:
///   <c>POST /api/tasks/internal/flow-sla-update</c>
///   <c>POST /api/tasks/internal/flow-queue-assign/{tenantId}/{taskId}</c>
/// </para>
///
/// <para>
/// The tenant ID is sourced from the <c>X-Tenant-Id</c> request header, which the
/// caller must set before passing the request to the pipeline. When the issuer is not
/// configured, the request is sent without any Authorization header and will result in
/// a 401 from the Task service — intentional: misconfigured environments must fail
/// loudly rather than silently skip the push.
/// </para>
/// </summary>
public sealed class FlowTaskInternalAuthHandler : DelegatingHandler
{
    private readonly IServiceTokenIssuer                     _issuer;
    private readonly ILogger<FlowTaskInternalAuthHandler>    _logger;

    public FlowTaskInternalAuthHandler(
        IServiceTokenIssuer                  issuer,
        ILogger<FlowTaskInternalAuthHandler> logger)
    {
        _issuer = issuer;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
    {
        if (!_issuer.IsConfigured)
        {
            _logger.LogWarning(
                "FlowTaskInternalAuthHandler: IServiceTokenIssuer is not configured — request to '{Path}' will have no auth header and will likely fail.",
                request.RequestUri?.PathAndQuery);
            return await base.SendAsync(request, cancellationToken);
        }

        // Tenant ID sourced from X-Tenant-Id header (set by caller).
        // For cross-tenant internal calls (e.g. TASK-FLOW-03 SLA batch read)
        // where there is no per-tenant context, fall back to the well-known
        // "SYSTEM" principal so the Task service InternalService policy still
        // validates the JWT signature + role claim without needing a real tenant.
        request.Headers.TryGetValues("X-Tenant-Id", out var vals);
        var tenantId = vals?.FirstOrDefault();
        if (string.IsNullOrEmpty(tenantId))
            tenantId = "SYSTEM";

        try
        {
            var token = _issuer.IssueToken(tenantId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FlowTaskInternalAuthHandler: failed to mint service token for tenant {TenantId} — request to '{Path}' will have no auth header.",
                tenantId, request.RequestUri?.PathAndQuery);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
