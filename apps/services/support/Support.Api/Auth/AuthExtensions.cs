using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Support.Api.Auth;

public static class AuthExtensions
{
    public const string TestScheme = "TestAuth";

    public static IServiceCollection AddSupportAuth(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var defaultScheme = env.IsEnvironment("Testing")
            ? TestScheme
            : JwtBearerDefaults.AuthenticationScheme;

        var authBuilder = services.AddAuthentication(defaultScheme);

        if (env.IsEnvironment("Testing"))
        {
            authBuilder.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                TestScheme, _ => { });
        }
        else
        {
            var jwtSection = config.GetSection("Jwt");
            var issuer     = jwtSection["Issuer"]   ?? "legalsynq-identity";
            var audience   = jwtSection["Audience"] ?? "legalsynq-platform";
            var signingKey = jwtSection["SigningKey"]
                ?? throw new InvalidOperationException(
                    "Jwt:SigningKey is not configured. " +
                    "Set the Jwt__SigningKey environment variable (Replit secret). " +
                    "Refusing to start without a verified signing strategy.");

            if (signingKey.Length < 32)
                throw new InvalidOperationException(
                    "Jwt:SigningKey must be at least 32 characters long.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            authBuilder.AddJwtBearer(o =>
            {
                o.MapInboundClaims    = false;
                o.RequireHttpsMetadata = false;

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = issuer,
                    ValidateAudience         = true,
                    ValidAudience            = audience,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey  = true,
                    IssuerSigningKey          = key,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                    // With MapInboundClaims = false the JWT "role" claim stays as
                    // the raw key "role" — RoleClaimType must match that key so
                    // RequireRole() / IsInRole() finds the claim correctly.
                    RoleClaimType            = "role",
                    NameClaimType            = "sub",
                };

                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        var lf  = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var log = lf.CreateLogger("Support.Auth");
                        var roles  = ctx.Principal?.FindAll("role").Select(c => c.Value).ToList() ?? [];
                        var tenant = ctx.Principal?.FindFirst("tenant_id")?.Value ?? "(none)";
                        var sub    = ctx.Principal?.FindFirst("sub")?.Value ?? "(none)";
                        log.LogInformation(
                            "JWT validated — sub={Sub} roles=[{Roles}] tenant_id={Tenant}",
                            sub, string.Join(",", roles), tenant);
                        return Task.CompletedTask;
                    },
                    OnForbidden = ctx =>
                    {
                        var lf  = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var log = lf.CreateLogger("Support.Auth");
                        var roles  = ctx.HttpContext.User?.FindAll("role").Select(c => c.Value).ToList() ?? [];
                        var tenant = ctx.HttpContext.User?.FindFirst("tenant_id")?.Value ?? "(none)";
                        log.LogWarning(
                            "SUPPORT-AUTH: 403 Forbidden — path={Path} roles=[{Roles}] tenant_id={Tenant}",
                            ctx.HttpContext.Request.Path, string.Join(",", roles), tenant);
                        return Task.CompletedTask;
                    },
                    OnChallenge = ctx =>
                    {
                        var lf  = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var log = lf.CreateLogger("Support.Auth");
                        log.LogWarning(
                            "SUPPORT-AUTH: 401 Challenge — path={Path} error={Error} errorDesc={Desc}",
                            ctx.HttpContext.Request.Path, ctx.Error, ctx.ErrorDescription);
                        return Task.CompletedTask;
                    },
                };
            });
        }

        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(SupportPolicies.SupportRead,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.All));
            opts.AddPolicy(SupportPolicies.SupportWrite,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.All));
            opts.AddPolicy(SupportPolicies.SupportManage,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.Managers));
            opts.AddPolicy(SupportPolicies.SupportInternal,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.InternalStaff));
            opts.AddPolicy(SupportPolicies.CustomerAccess,
                p => p.RequireAuthenticatedUser().RequireRole(SupportRoles.ExternalCustomer));
        });

        return services;
    }
}
