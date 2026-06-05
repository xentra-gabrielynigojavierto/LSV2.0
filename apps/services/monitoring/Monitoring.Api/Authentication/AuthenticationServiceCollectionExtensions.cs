using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Monitoring.Api.Authentication;

/// <summary>
/// Wires platform-standard dual-scheme JWT authentication into the Monitoring API.
///
/// <para><b>Scheme 1 — Bearer (user JWTs, HS256):</b><br/>
/// Validates tokens issued by the platform Identity service.
/// Config: <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>, <c>Jwt:SigningKey</c>.
/// Used by human operators accessing admin endpoints via the Control Center.</para>
///
/// <para><b>Scheme 2 — ServiceToken (machine-to-machine JWTs, HS256):</b><br/>
/// Validates service tokens minted by platform services via
/// <c>BuildingBlocks.Authentication.ServiceTokens</c>.
/// Signing key: <c>FLOW_SERVICE_TOKEN_SECRET</c> env var (same shared secret
/// used by all platform services). Subject must start with <c>service:</c>.</para>
///
/// <para><b>MON-INT-01-003:</b> Replaces the previous RS256-only scheme that
/// used an embedded RSA public key and a custom <c>Authentication:Jwt</c>
/// configuration section. The new model is identical to every other platform
/// service (Liens, Notifications, etc.).</para>
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoringAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");

        var userSigningKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. " +
                "Set it via the FLOW_SERVICE_TOKEN_SECRET-equivalent secret or appsettings.");

        // Service-token key: prefer FLOW_SERVICE_TOKEN_SECRET env var,
        // then fall back to ServiceTokens:SigningKey config.
        var serviceTokenKey =
            Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
            ?? configuration[$"{ServiceTokenOptions.SectionName}:SigningKey"]
            ?? string.Empty;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

            // ── Scheme 1: user JWTs from the platform Identity service ────────
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims    = false;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSection["Issuer"],
                    ValidAudience            = jwtSection["Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(
                                                  Encoding.UTF8.GetBytes(userSigningKey)),
                    RoleClaimType            = "role",
                    NameClaimType            = "sub",
                    ClockSkew                = TimeSpan.Zero,
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Monitoring.Api.Auth")
                            .LogWarning("Bearer JWT authentication failed: {Reason}",
                                ctx.Exception.GetType().Name);
                        return Task.CompletedTask;
                    },
                };
            })

            // ── Scheme 2: service-to-service tokens (FLOW_SERVICE_TOKEN_SECRET) ─
            // Accepted from any platform service that mints a ServiceToken
            // with subject=service:* and audience=monitoring-service.
            .AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
            {
                options.MapInboundClaims     = false;
                options.RequireHttpsMetadata  = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey  = !string.IsNullOrWhiteSpace(serviceTokenKey),
                    RequireSignedTokens      = true,
                    RequireExpirationTime    = true,
                    ValidIssuer              = ServiceTokenAuthenticationDefaults.DefaultIssuer,
                    ValidAudiences           = ["monitoring-service", "legalsynq-services",
                                               ServiceTokenAuthenticationDefaults.DefaultAudience],
                    IssuerSigningKey         = string.IsNullOrWhiteSpace(serviceTokenKey)
                                                  ? null
                                                  : new SymmetricSecurityKey(
                                                        Encoding.UTF8.GetBytes(serviceTokenKey)),
                    NameClaimType            = "sub",
                    RoleClaimType            = ClaimTypes.Role,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        var sub = ctx.Principal?.FindFirst("sub")?.Value;
                        if (string.IsNullOrWhiteSpace(sub) ||
                            !sub.StartsWith("service:", StringComparison.Ordinal))
                        {
                            ctx.Fail("Service token must have a subject starting with 'service:'.");
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = ctx =>
                    {
                        ctx.HttpContext.RequestServices
                            .GetService<ILoggerFactory>()
                            ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme)
                            ?.LogWarning(ctx.Exception,
                                "ServiceToken authentication failed. Path={Path}",
                                ctx.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            // MonitoringAdmin: grants access to write/admin endpoints.
            // Requires a valid user JWT with the PlatformAdmin role only.
            // Service tokens are intentionally excluded from admin write access;
            // they may authenticate against read endpoints but must never be
            // able to register monitoring targets or resolve alerts.
            options.AddPolicy(MonitoringPolicies.AdminWrite, policy =>
                policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireRole("PlatformAdmin"));
        });

        return services;
    }
}
