using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Support.Api.Auth;

/// <summary>
/// Test-only authentication handler. Reads identity from request headers so
/// integration tests can target endpoints without issuing real JWTs.
///
/// Headers:
///   X-Test-NoAuth: "true"     -> NoResult (causes 401 on protected endpoints)
///   X-Tenant-Id:   "&lt;id&gt;"  -> tenant_id claim
///   X-Test-Sub:    "&lt;id&gt;"  -> sub claim          (default: "test-user")
///   X-Test-Roles:  "Role1,Role2" -> role claims    (default: all support roles)
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.Equals(Request.Headers["X-Test-NoAuth"].FirstOrDefault(), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        var sub = Request.Headers["X-Test-Sub"].FirstOrDefault() ?? "test-user";
        claims.Add(new Claim("sub", sub));

        var tenant = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var skipTenant = string.Equals(
            Request.Headers["X-Test-NoTenantClaim"].FirstOrDefault(), "true",
            StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(tenant) && !skipTenant)
            claims.Add(new Claim("tenant_id", tenant));

        var rolesHeader = Request.Headers["X-Test-Roles"].FirstOrDefault();
        var roles = !string.IsNullOrWhiteSpace(rolesHeader)
            ? rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : SupportRoles.All;
        foreach (var r in roles) claims.Add(new Claim("role", r));

        var identity = new ClaimsIdentity(claims, AuthExtensions.TestScheme, "sub", "role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthExtensions.TestScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
