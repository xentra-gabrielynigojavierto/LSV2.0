using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-HARDEN-A1 — distinguishes the three caller modes that hit the
/// Flow execution surface so controllers can branch policy decisions
/// (e.g. "skip per-product capability check when called by a service
/// token because the product already enforced it before forwarding").
/// </summary>
public enum CallerType
{
    Unknown = 0,
    User    = 1,
    Service = 2,
    ServiceOnBehalfOfUser = 3,
}

/// <summary>
/// LS-FLOW-HARDEN-A1 — projection of the current
/// <see cref="ClaimsPrincipal"/> into the fields any execution-surface
/// handler needs for ownership checks, capability gating, and
/// structured logs. Resolved per request.
/// </summary>
public sealed record CallerContext(
    CallerType Type,
    string? TenantId,
    string? Subject,
    string? Actor)
{
    public static CallerContext Empty { get; } = new(CallerType.Unknown, null, null, null);

    public bool IsUser    => Type == CallerType.User;
    public bool IsService => Type == CallerType.Service || Type == CallerType.ServiceOnBehalfOfUser;
}

/// <summary>
/// LS-FLOW-HARDEN-A1 — resolves the current request's
/// <see cref="CallerContext"/>. Default implementation reads from
/// <see cref="IHttpContextAccessor"/>; tests can replace it.
/// </summary>
public interface ICallerContextAccessor
{
    CallerContext Current { get; }
}

public sealed class CallerContextAccessor : ICallerContextAccessor
{
    private readonly IHttpContextAccessor _http;

    public CallerContextAccessor(IHttpContextAccessor http) => _http = http;

    public CallerContext Current
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return CallerContext.Empty;

            // tenant: prefer the platform-standard "tenant_id" claim, fall back to "tid".
            var tenantId = user.FindFirst("tenant_id")?.Value
                           ?? user.FindFirst("tid")?.Value;

            var sub = user.FindFirst("sub")?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // service tokens always carry sub="service:<name>" (see ServiceTokenIssuer).
            var isService = !string.IsNullOrEmpty(sub) &&
                            sub.StartsWith("service:", StringComparison.Ordinal);

            var actor = user.FindFirst(ServiceTokenAuthenticationDefaults.ActorClaim)?.Value;

            CallerType type = isService
                ? (string.IsNullOrEmpty(actor) ? CallerType.Service : CallerType.ServiceOnBehalfOfUser)
                : CallerType.User;

            return new CallerContext(type, tenantId, sub, actor);
        }
    }
}
