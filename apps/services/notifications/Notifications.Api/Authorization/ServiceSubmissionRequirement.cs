using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Notifications.Api.Authorization;

/// <summary>
/// LS-NOTIF-CORE-021 — authorization requirement for the
/// <c>POST /v1/notifications</c> producer endpoint.
///
/// <para>
/// The handler succeeds only when the caller presents a JWT that carries a
/// non-empty <c>svc</c> claim, identifying it as an internal service token.
/// Ordinary user JWTs (no <c>svc</c> claim) and unauthenticated callers are
/// rejected so that the notification producer is restricted to backend
/// services and cannot be abused by low-privilege tenant users.
/// </para>
/// </summary>
public sealed class ServiceSubmissionRequirement : IAuthorizationRequirement { }

/// <summary>
/// Handles <see cref="ServiceSubmissionRequirement"/>.
/// Registered as a singleton in <c>Program.cs</c>.
/// </summary>
public sealed class ServiceSubmissionHandler
    : AuthorizationHandler<ServiceSubmissionRequirement>
{
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ServiceSubmissionHandler> _logger;

    public ServiceSubmissionHandler(
        IHttpContextAccessor http,
        ILogger<ServiceSubmissionHandler> logger)
    {
        _http   = http;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServiceSubmissionRequirement requirement)
    {
        var httpCtx = _http.HttpContext;

        if (context.User.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "POST /v1/notifications rejected: unauthenticated caller. " +
                "Path={Path} RemoteIp={RemoteIp}",
                httpCtx?.Request.Path.Value,
                httpCtx?.Connection.RemoteIpAddress?.ToString());

            return Task.CompletedTask;
        }

        var sub         = context.User.FindFirst("sub")?.Value ?? "(unknown)";
        var serviceName = context.User.FindFirst("svc")?.Value;
        var tenantId    = context.User.FindFirst("tenant_id")?.Value ?? "(unknown)";

        if (string.IsNullOrEmpty(serviceName))
        {
            _logger.LogWarning(
                "POST /v1/notifications rejected: authenticated caller lacks svc claim " +
                "(ordinary user token). Sub={Sub} TenantId={TenantId} " +
                "Path={Path} RemoteIp={RemoteIp}",
                sub, tenantId,
                httpCtx?.Request.Path.Value,
                httpCtx?.Connection.RemoteIpAddress?.ToString());

            return Task.CompletedTask;
        }

        _logger.LogDebug(
            "Service submission authorised via service JWT. " +
            "ServiceName={ServiceName} Sub={Sub} TenantId={TenantId}",
            serviceName, sub, tenantId);

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
