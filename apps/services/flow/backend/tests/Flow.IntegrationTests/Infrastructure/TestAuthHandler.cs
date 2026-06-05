using System.Security.Claims;
using System.Text.Encodings.Web;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — synthesises a <see cref="ClaimsPrincipal"/> from
/// the <c>X-Test-*</c> request headers. Designed to exercise the production
/// auth pipeline as wired (multi-scheme + tenant claim filter + capability
/// policies) without minting JWTs.
///
/// Auth outcome:
/// <list type="bullet">
///   <item>No <c>X-Test-Sub</c> header → <see cref="HandleAuthenticateAsync"/>
///         returns <see cref="AuthenticateResult.NoResult"/>; the framework
///         reports 401 Challenge against any <c>[Authorize]</c> endpoint.</item>
///   <item>Any sub starting with <c>service:</c> produces a service-token
///         shaped principal (matches what
///         <see cref="ServiceTokenIssuer"/> mints in production).</item>
///   <item>Otherwise a user-shaped principal with role + permissions +
///         product-role claims.</item>
/// </list>
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub = Request.Headers[TestAuthDefaults.SubHeader].ToString();
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var tenantId = Request.Headers[TestAuthDefaults.TenantHeader].ToString();
        var role     = Request.Headers[TestAuthDefaults.RoleHeader].ToString();
        var actor    = Request.Headers[TestAuthDefaults.ActorHeader].ToString();
        var aud      = Request.Headers[TestAuthDefaults.AudHeader].ToString();

        var perms = Split(Request.Headers[TestAuthDefaults.PermissionsHeader].ToString());
        var pRoles = Split(Request.Headers[TestAuthDefaults.ProductRolesHeader].ToString());

        var claims = new List<Claim>
        {
            new("sub", sub),
            new(ClaimTypes.NameIdentifier, sub),
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim("tenant_id", tenantId));

        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (!string.IsNullOrWhiteSpace(actor))
            claims.Add(new Claim(ServiceTokenAuthenticationDefaults.ActorClaim, actor));

        if (!string.IsNullOrWhiteSpace(aud))
            claims.Add(new Claim("aud", aud));

        foreach (var p in perms)
            claims.Add(new Claim("permissions", p));

        foreach (var pr in pRoles)
            claims.Add(new Claim("product_roles", pr));

        var identity  = new ClaimsIdentity(claims, TestAuthDefaults.Scheme, "sub", ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, TestAuthDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static IEnumerable<string> Split(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
