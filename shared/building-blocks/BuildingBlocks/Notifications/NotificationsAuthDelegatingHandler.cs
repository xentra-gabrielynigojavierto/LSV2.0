using System.Net.Http.Headers;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Notifications;

/// <summary>
/// LS-NOTIF-CORE-021 — optional delegating handler that mints a
/// short-lived service JWT for outbound calls to the Notifications
/// service endpoint (<c>POST /v1/notifications</c>).
///
/// <para>
/// The handler reads the <c>X-Tenant-Id</c> header already set by the
/// caller, mints a service token carrying that tenant as a claim, and
/// injects <c>Authorization: Bearer &lt;token&gt;</c>.  The
/// <c>X-Tenant-Id</c> header is left intact so the Notifications server
/// can fall back to the header path when no JWT is present (dev / unconfigured).
/// </para>
///
/// <para>
/// When <see cref="IServiceTokenIssuer.IsConfigured"/> is <c>false</c>
/// (signing secret not set) the handler is a no-op: requests pass
/// through without a Bearer token and the Notifications server accepts
/// them in LEGACY SUBMISSION mode, logging a warning on its side.
/// </para>
/// </summary>
public sealed class NotificationsAuthDelegatingHandler : DelegatingHandler
{
    private readonly IServiceTokenIssuer _issuer;
    private readonly ILogger<NotificationsAuthDelegatingHandler> _logger;

    public NotificationsAuthDelegatingHandler(
        IServiceTokenIssuer issuer,
        ILogger<NotificationsAuthDelegatingHandler> logger)
    {
        _issuer = issuer;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
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
                        "NotificationsAuthDelegatingHandler could not mint service token; " +
                        "falling back to legacy X-Tenant-Id header. TenantId={TenantId}",
                        tenantId);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
