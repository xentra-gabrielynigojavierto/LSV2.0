using System.Security.Claims;

namespace Support.Api.Auth;

/// <summary>
/// Resolved acting principal for audit/event emission.
/// Derived from JWT claims (or the test auth handler in Testing env).
/// </summary>
public sealed record AuditActor(
    string? UserId,
    string? Name,
    string? Email,
    IReadOnlyList<string> Roles);

/// <summary>
/// Per-request HTTP correlation metadata used to enrich audit events.
/// </summary>
public sealed record AuditRequestContext(
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent);

public interface IActorAccessor
{
    /// <summary>Acting principal for the current request, or empty when anonymous.</summary>
    AuditActor Actor { get; }

    /// <summary>Best-effort request metadata for audit correlation.</summary>
    AuditRequestContext Request { get; }
}

/// <summary>
/// Default <see cref="IActorAccessor"/> backed by <see cref="IHttpContextAccessor"/>.
/// Reads <c>sub</c>, <c>email</c>, and <c>role</c> claims (with .NET ClaimTypes
/// fallbacks). Outside of an HTTP request returns empty values; callers that
/// want to mark an action as "system" should set the audit actor explicitly.
/// </summary>
public sealed class HttpContextActorAccessor : IActorAccessor
{
    private readonly IHttpContextAccessor _http;

    public HttpContextActorAccessor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public AuditActor Actor
    {
        get
        {
            var ctx = _http.HttpContext;
            var user = ctx?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new AuditActor(null, null, null, Array.Empty<string>());
            }

            var sub = user.FindFirst("sub")?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = user.FindFirst("name")?.Value
                       ?? user.FindFirst(ClaimTypes.Name)?.Value
                       ?? user.Identity?.Name;
            var email = user.FindFirst("email")?.Value
                        ?? user.FindFirst(ClaimTypes.Email)?.Value;

            // Roles can arrive as singular `role`, plural `roles`, or via the
            // .NET ClaimTypes.Role mapping. `roles` may itself be space- or
            // comma-delimited in some JWT issuers, so we expand both.
            var roleClaims = user.FindAll("role").Select(c => c.Value)
                .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            var pluralRoles = user.FindAll("roles")
                .SelectMany(c => (c.Value ?? string.Empty)
                    .Split(new[] { ' ', ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            var roles = roleClaims
                .Concat(pluralRoles)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AuditActor(sub, name, email, roles);
        }
    }

    public AuditRequestContext Request
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is null)
            {
                return new AuditRequestContext(null, null, null);
            }

            var correlation = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                              ?? ctx.Request.Headers["X-Request-Id"].FirstOrDefault()
                              ?? ctx.TraceIdentifier;
            var ip = ctx.Connection?.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers["User-Agent"].FirstOrDefault();
            return new AuditRequestContext(correlation, ip, ua);
        }
    }
}
