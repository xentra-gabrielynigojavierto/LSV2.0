using System.Net.Http.Headers;
using System.Security.Claims;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Http;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// LS-FLOW-E13.1 — chooses the <c>Authorization</c> header to attach
/// when Flow calls the audit service over HTTP. Used by both the audit
/// write adapter (<see cref="HttpAuditAdapter"/>) and the read adapter
/// (<see cref="HttpAuditQueryAdapter"/>) so the audit service's
/// per-caller scope checks are honoured upstream instead of treating
/// every Flow → Audit hop as an anonymous internal call.
///
/// Selection order:
/// <list type="number">
///   <item>If the current request carries a <c>Bearer</c> token, forward
///         it verbatim. This preserves the originating operator's
///         identity (tenant, user, roles) so the audit service's
///         <c>QueryAuthorizer</c> applies the same scope rules it would
///         for a direct call.</item>
///   <item>Otherwise — including for background outbox writes that have
///         no <see cref="HttpContext"/> — mint a short-lived service
///         token via <see cref="IServiceTokenIssuer"/> when one is
///         configured, using the audit event's tenant/user as fallback
///         identity for the <c>actor</c> claim.</item>
///   <item>If neither is available (issuer unconfigured AND no caller
///         bearer), no header is attached. The audit service's anonymous
///         mode (<c>QueryAuth:Mode = "None"</c>, e.g. local dev) keeps
///         working unchanged.</item>
/// </list>
/// </summary>
public sealed class AuditAuthHeaderProvider
{
    private readonly IHttpContextAccessor _http;
    private readonly IServiceTokenIssuer? _issuer;

    public AuditAuthHeaderProvider(
        IHttpContextAccessor http,
        IServiceTokenIssuer? issuer = null)
    {
        _http   = http;
        _issuer = issuer;
    }

    public AuthenticationHeaderValue? GetHeader(
        string? fallbackTenantId = null,
        string? fallbackUserId   = null)
    {
        var ctx = _http.HttpContext;

        // 1) Caller's bearer wins — preserves operator identity for
        //    upstream scope checks. Service tokens drop role/permission
        //    detail, so we only mint one when no user bearer is present.
        if (ctx is not null)
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(auth) &&
                auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return new AuthenticationHeaderValue(
                    "Bearer", auth["Bearer ".Length..].Trim());
            }
        }

        // 2) No caller bearer — mint an M2M token when configured.
        if (_issuer is not null && _issuer.IsConfigured)
        {
            var (tenantId, userId) = ExtractTenantAndUser(ctx);
            tenantId ??= fallbackTenantId;
            userId   ??= fallbackUserId;

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var token = _issuer.IssueToken(tenantId!, userId);
                return new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // 3) Anonymous fallback — keeps Audit:Mode=None (dev) working.
        return null;
    }

    private static (string? TenantId, string? UserId) ExtractTenantAndUser(HttpContext? ctx)
    {
        if (ctx?.User is not ClaimsPrincipal user ||
            user.Identity?.IsAuthenticated != true)
        {
            return (null, null);
        }

        var tenantId = user.FindFirst("tenant_id")?.Value
                       ?? user.FindFirst("tid")?.Value;
        var userId   = user.FindFirst("sub")?.Value
                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return (tenantId, userId);
    }
}
