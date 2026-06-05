using System.Security.Claims;
using System.Text;
using BuildingBlocks.FlowClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 / LS-FLOW-HARDEN-A1 — DI helpers for service-token
/// issuance (product side) and validation (Flow side).
/// </summary>
public static class ServiceTokenServiceCollectionExtensions
{
    /// <summary>
    /// Register an <see cref="IServiceTokenIssuer"/> bound from the
    /// <c>ServiceTokens</c> configuration section. The shared secret is
    /// preferred from the <c>FLOW_SERVICE_TOKEN_SECRET</c> environment
    /// variable, then from <c>ServiceTokens:SigningKey</c>.
    /// </summary>
    public static IServiceCollection AddServiceTokenIssuer(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOptions<ServiceTokenOptions>()
            .Bind(configuration.GetSection(ServiceTokenOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.SigningKey))
                {
                    o.SigningKey = Environment.GetEnvironmentVariable(
                        ServiceTokenAuthenticationDefaults.SecretEnvVar) ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(o.ServiceName) || o.ServiceName == "unknown-service")
                {
                    o.ServiceName = serviceName;
                }
            });

        services.AddSingleton<IServiceTokenIssuer, ServiceTokenIssuer>();

        // LS-FLOW-HARDEN-A1 — caller-context accessor lives next to the
        // issuer so any service that registers the issuer (and therefore
        // can mint a token) can also classify inbound callers when it
        // forwards on behalf of users.
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICallerContextAccessor, CallerContextAccessor>();
        return services;
    }

    /// <summary>
    /// Add a second <see cref="JwtBearer"/> scheme
    /// (<see cref="ServiceTokenAuthenticationDefaults.Scheme"/>) that
    /// validates HS256 service tokens.
    ///
    /// <para>
    /// LS-FLOW-HARDEN-A1 hardening:
    /// <list type="bullet">
    ///   <item><c>RequireSignedTokens = true</c> — refuse <c>alg=none</c>.</item>
    ///   <item><c>ClockSkew = 30s</c> — keep small to limit replay window.</item>
    ///   <item>Custom <c>OnTokenValidated</c> rejects tokens without a
    ///         <c>service:</c> subject or without a tenant claim
    ///         (<c>tenant_id</c>/<c>tid</c>) — these are required by
    ///         every Flow execution path.</item>
    ///   <item>Optional <paramref name="failFastIfMissingSecret"/> — when
    ///         true and no signing key is present, throws on startup so
    ///         non-Development hosts cannot silently boot with a no-op
    ///         service-token validator.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddServiceTokenBearer(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        bool failFastIfMissingSecret = false)
    {
        var section = configuration.GetSection(ServiceTokenOptions.SectionName);
        var signingKey = section["SigningKey"]
                         ?? Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
                         ?? string.Empty;
        var issuer    = section["Issuer"]   ?? ServiceTokenAuthenticationDefaults.DefaultIssuer;
        var audience  = section["Audience"] ?? ServiceTokenAuthenticationDefaults.DefaultAudience;

        if (failFastIfMissingSecret && string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                $"Service-token auth is required but no signing key is configured. " +
                $"Set the {ServiceTokenAuthenticationDefaults.SecretEnvVar} environment variable " +
                $"or the ServiceTokens:SigningKey configuration value.");
        }
        if (failFastIfMissingSecret && signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"Service-token signing key must be at least 32 characters. " +
                $"The current {ServiceTokenAuthenticationDefaults.SecretEnvVar} value is too short.");
        }

        return builder.AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = false; // tokens are validated against an in-memory key, not a JWKS endpoint
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(signingKey),
                RequireSignedTokens      = true,
                RequireExpirationTime    = true,
                ValidIssuer              = issuer,
                ValidAudience            = audience,
                IssuerSigningKey         = string.IsNullOrWhiteSpace(signingKey)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                NameClaimType            = "sub",
                // ServiceTokenIssuer uses new JwtSecurityToken(claims:...) which does NOT apply
                // DefaultOutboundClaimTypeMap, so ClaimTypes.Role is written as the full XML URI
                // into the JWT payload. With MapInboundClaims = false the claim type is preserved
                // as-is, so RoleClaimType must match the full ClaimTypes.Role URI for IsInRole
                // to find it.
                RoleClaimType            = ClaimTypes.Role,
                ClockSkew                = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                // LS-FLOW-HARDEN-A1: enforce semantic claim requirements.
                // Signature/issuer/audience/expiry have already passed at
                // this point — here we ensure the token is the right SHAPE
                // for an execution-surface call.
                OnTokenValidated = ctx =>
                {
                    var principal = ctx.Principal;
                    var sub = principal?.FindFirst("sub")?.Value;

                    if (string.IsNullOrWhiteSpace(sub) ||
                        !sub.StartsWith("service:", StringComparison.Ordinal))
                    {
                        FailWith(ctx, FlowErrorCodes.InvalidServiceToken,
                            "Service token is missing a service:* subject.");
                        return Task.CompletedTask;
                    }

                    var tenantId = principal?.FindFirst("tenant_id")?.Value
                                   ?? principal?.FindFirst(ServiceTokenAuthenticationDefaults.TenantClaim)?.Value;
                    if (string.IsNullOrWhiteSpace(tenantId))
                    {
                        FailWith(ctx, FlowErrorCodes.MissingTenantContext,
                            "Service token is missing a tenant claim (tenant_id/tid).");
                        return Task.CompletedTask;
                    }

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetService<ILoggerFactory>()
                        ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme);
                    logger?.LogWarning(ctx.Exception,
                        "ServiceToken authentication failed code={Code} path={Path}",
                        FlowErrorCodes.InvalidServiceToken, ctx.HttpContext.Request.Path);
                    return Task.CompletedTask;
                }
            };
        });
    }

    private static void FailWith(
        Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext ctx,
        string code, string message)
    {
        var logger = ctx.HttpContext.RequestServices
            .GetService<ILoggerFactory>()
            ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme);
        logger?.LogWarning(
            "ServiceToken rejected code={Code} reason={Reason} path={Path}",
            code, message, ctx.HttpContext.Request.Path);
        ctx.Fail(message);
    }
}

