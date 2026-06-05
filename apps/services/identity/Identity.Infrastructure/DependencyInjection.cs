using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.Notifications;
using Identity.Application;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Infrastructure.Auth;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddAuditEventClient(configuration);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<AuthorizationService>();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<IScopedAuthorizationService, ScopedAuthorizationService>();

        services.Configure<Route53DnsOptions>(configuration.GetSection("Route53"));
        services.AddSingleton<IDnsService, Route53DnsService>();

        services.Configure<TenantVerificationOptions>(configuration.GetSection("TenantVerification"));
        services.AddScoped<ITenantVerificationService, TenantVerificationService>();

        services.Configure<VerificationRetryOptions>(configuration.GetSection("VerificationRetry"));
        services.AddScoped<IVerificationRetryService, VerificationRetryService>();
        services.AddHostedService<VerificationRetryBackgroundService>();

        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        services.AddHttpClient("CareConnectInternal", client =>
        {
            var ccUrl = configuration["CareConnect:InternalUrl"] ?? "http://localhost:5003";
            client.BaseAddress = new Uri(ccUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IProductProvisioningHandler, CareConnectProvisioningHandler>();
        services.AddScoped<IProductProvisioningService, ProductProvisioningService>();

        services.AddScoped<IAuditPublisher, AuditPublisher>();

        // Notifications cache invalidation client — fire-and-observe HTTP call
        // to notifications when role/membership events occur, so role-addressed
        // notifications reflect the new membership immediately rather than
        // waiting for the notifications service's TTL-based cache to expire.
        services.AddOptions<NotificationsServiceOptions>()
                .Bind(configuration.GetSection(NotificationsServiceOptions.SectionName));

        // LS-NOTIF-CORE-024: Service-JWT auth for outbound Notifications calls.
        // Sources the signing key from FLOW_SERVICE_TOKEN_SECRET env var (same
        // secret used by the Notifications service for inbound token validation).
        // When the secret is absent (dev/unconfigured), NotificationsAuthDelegatingHandler
        // is a no-op and requests fall through to the legacy X-Tenant-Id path.
        services.AddServiceTokenIssuer(configuration, "identity");
        services.AddTransient<NotificationsAuthDelegatingHandler>();

        // LS-NOTIF-CORE-024: Wire the auth handler onto the named HTTP client so
        // all callers — both the transactional email client and the cache client —
        // automatically include a service JWT when FLOW_SERVICE_TOKEN_SECRET is set.
        services.AddHttpClient("NotificationsService")
                .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();

        // LS-ID-TNT-006 / LS-NOTIF-CORE-024: Transactional email client — calls
        // POST /v1/notifications on the Notifications service to deliver
        // password-reset and invitation emails.  Returns EmailConfigured=false when
        // NotificationsService:BaseUrl is not set so callers can apply the correct
        // non-email fallback without treating the absence as a delivery failure.
        services.AddScoped<INotificationsEmailClient, NotificationsEmailClient>();
        var notificationsBaseUrl = configuration[$"{NotificationsServiceOptions.SectionName}:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(notificationsBaseUrl))
        {
            // Same singleton serves both interfaces so the diagnostics endpoint
            // reports counters from the instance that actually issues the calls.
            services.AddSingleton<NotificationsCacheClient>();
            services.AddSingleton<INotificationsCacheClient>(sp =>
                sp.GetRequiredService<NotificationsCacheClient>());
            services.AddSingleton<INotificationsCacheClientDiagnostics>(sp =>
                sp.GetRequiredService<NotificationsCacheClient>());
        }
        else
        {
            services.AddSingleton<NoOpNotificationsCacheClient>();
            services.AddSingleton<INotificationsCacheClient>(sp =>
                sp.GetRequiredService<NoOpNotificationsCacheClient>());
            services.AddSingleton<INotificationsCacheClientDiagnostics>(sp =>
                sp.GetRequiredService<NoOpNotificationsCacheClient>());
        }
        // ── TENANT-B07: Identity → Tenant dual-write adapter ─────────────────────
        // Registers the real HttpTenantSyncAdapter when Features:TenantDualWriteEnabled=true,
        // otherwise registers IdentityNoOpTenantSyncAdapter (zero runtime cost, debug log only).
        var dualWriteEnabled = configuration.GetValue<bool>("Features:TenantDualWriteEnabled", false);
        var dualWriteStrict  = configuration.GetValue<bool>("Features:TenantDualWriteStrictMode", false);

        if (dualWriteEnabled)
        {
            var tenantInternalUrl = configuration.GetValue<string>("TenantService:InternalUrl")
                                    ?? "http://127.0.0.1:5005";
            var syncSecret = configuration.GetValue<string>("TenantService:SyncSecret") ?? string.Empty;

            services.AddHttpClient("TenantSyncInternal", client =>
            {
                client.BaseAddress = new Uri(tenantInternalUrl);
                client.Timeout     = TimeSpan.FromSeconds(5);
                if (!string.IsNullOrWhiteSpace(syncSecret))
                    client.DefaultRequestHeaders.Add("X-Sync-Token", syncSecret);
            });

            services.AddScoped<ITenantSyncAdapter>(sp => new HttpTenantSyncAdapter(
                sp.GetRequiredService<IHttpClientFactory>(),
                dualWriteStrict,
                sp.GetRequiredService<ILogger<HttpTenantSyncAdapter>>()));
        }
        else
        {
            services.AddScoped<ITenantSyncAdapter, IdentityNoOpTenantSyncAdapter>();
        }

        // Logo registration client — calls Documents service to set IsPublishedAsLogo=true
        // after Identity stores a tenant's logo document ID.
        services.AddHttpClient("DocumentsInternal", client =>
        {
            var docsUrl = configuration["DocumentsService:InternalUrl"] ?? "http://127.0.0.1:5006";
            client.BaseAddress = new Uri(docsUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddScoped<ITenantProductEntitlementService, TenantProductEntitlementService>();
        services.AddScoped<IUserProductAccessService, UserProductAccessService>();
        services.AddScoped<IUserRoleAssignmentService, UserRoleAssignmentService>();
        services.AddScoped<IUserMembershipService, UserMembershipService>();   // BLK-ID-02
        services.AddScoped<IAccessSourceQueryService, AccessSourceQueryService>();
        services.AddScoped<IEffectiveAccessService, EffectiveAccessService>();
        services.AddScoped<IEffectivePermissionService, EffectivePermissionService>();
        services.AddScoped<IAuthorizationSimulationService, AuthorizationSimulationService>();

        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupMembershipService, GroupMembershipService>();
        services.AddScoped<IGroupProductAccessService, GroupProductAccessService>();
        services.AddScoped<IGroupRoleAssignmentService, GroupRoleAssignmentService>();

        services.Configure<PolicyCachingOptions>(configuration.GetSection("Authorization:PolicyCaching"));
        services.Configure<PolicyLoggingOptions>(configuration.GetSection("Authorization:PolicyLogging"));
        services.Configure<PolicyVersioningOptions>(configuration.GetSection("Authorization:PolicyVersioning"));
        services.AddSingleton<PolicyMetrics>();

        services.AddScoped<IAttributeProvider, DefaultAttributeProvider>();
        services.AddScoped<IPolicyEvaluationService, PolicyEvaluationService>();
        services.AddScoped<IPolicyResourceContextAccessor, HttpContextPolicyResourceContextAccessor>();

        AddPolicyInfrastructure(services, configuration);

        return services;
    }

    private static void AddPolicyInfrastructure(IServiceCollection services, IConfiguration configuration)
    {
        var cachingProvider = configuration["Authorization:PolicyCaching:Provider"] ?? "InMemory";
        var versioningProvider = configuration["Authorization:PolicyVersioning:Provider"] ?? "InMemory";
        var redisUrl = configuration["Authorization:Redis:Url"] ?? configuration["Redis:Url"] ?? "";
        var useRedis = string.Equals(cachingProvider, "Redis", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(versioningProvider, "Redis", StringComparison.OrdinalIgnoreCase);

        if (useRedis && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RedisPolicyVersionProvider>>();
                try
                {
                    return ConnectionMultiplexer.Connect(redisUrl);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis connection failed — distributed policy features will use in-memory fallback");
                    throw;
                }
            });
        }

        if (string.Equals(versioningProvider, "Redis", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IPolicyVersionProvider>(sp =>
            {
                try
                {
                    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                    return new RedisPolicyVersionProvider(redis, sp.GetRequiredService<ILogger<RedisPolicyVersionProvider>>(), sp.GetRequiredService<PolicyMetrics>());
                }
                catch
                {
                    return new InMemoryPolicyVersionProvider();
                }
            });
        }
        else
        {
            services.AddSingleton<IPolicyVersionProvider, InMemoryPolicyVersionProvider>();
        }

        if (string.Equals(cachingProvider, "Redis", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IPolicyEvaluationCache>(sp =>
            {
                try
                {
                    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                    return new RedisPolicyEvaluationCache(redis, sp.GetRequiredService<ILogger<RedisPolicyEvaluationCache>>());
                }
                catch
                {
                    return new InMemoryPolicyEvaluationCache(sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
                }
            });
        }
        else
        {
            services.AddSingleton<IPolicyEvaluationCache, InMemoryPolicyEvaluationCache>();
        }
    }
}
