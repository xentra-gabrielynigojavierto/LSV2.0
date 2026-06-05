using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenant.Application.Metrics;
using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Infrastructure.Data;
using Tenant.Infrastructure.Repositories;
using Tenant.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("Connection string 'TenantDb' is not configured.");

        services.AddDbContext<TenantDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddDbContextFactory<TenantDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))),
            lifetime: ServiceLifetime.Scoped);

        // ── TENANT-B08: In-process memory cache (BCL; no new package dependency) ──
        services.AddMemoryCache();

        // ── TENANT-B08: Runtime metrics singleton ─────────────────────────────
        services.AddSingleton<TenantRuntimeMetrics>();

        // ── Repositories ──────────────────────────────────────────────────────

        services.AddScoped<ITenantRepository,       TenantRepository>();
        services.AddScoped<IBrandingRepository,     BrandingRepository>();
        services.AddScoped<IDomainRepository,       DomainRepository>();
        services.AddScoped<IEntitlementRepository,  EntitlementRepository>();
        services.AddScoped<ICapabilityRepository,   CapabilityRepository>();
        services.AddScoped<ISettingRepository,      SettingRepository>();

        // ── Application services ──────────────────────────────────────────────

        services.AddScoped<ITenantService,          TenantService>();
        services.AddScoped<IBrandingService,        BrandingService>();
        services.AddScoped<IDomainService,          DomainService>();
        services.AddScoped<IResolutionService,      ResolutionService>();
        services.AddScoped<IEntitlementService,     EntitlementService>();
        services.AddScoped<ICapabilityService,      CapabilityService>();
        services.AddScoped<ISettingService,         SettingService>();
        services.AddScoped<IMigrationUtilityService, MigrationUtilityService>();
        services.AddScoped<ITenantSyncAdapter,       NoOpTenantSyncAdapter>();
        services.AddScoped<ITenantAdminService,      TenantAdminService>();

        // ── TENANT-B11: Identity compat adapter (read-through for sessionTimeoutMinutes) ──
        services.AddHttpClient("IdentityInternal", client =>
        {
            var identityUrl = configuration["IdentityService:InternalUrl"] ?? "http://127.0.0.1:5001";
            client.BaseAddress = new Uri(identityUrl);
            client.DefaultRequestHeaders.Add("X-Internal-Client", "tenant-service");
        });

        services.AddScoped<IIdentityCompatAdapter, HttpIdentityCompatAdapter>();

        // ── TENANT-B12: Identity provisioning adapter (canonical create, Tenant-first) ──
        // Shares the "IdentityInternal" named client already wired above.
        // Calls POST /api/internal/tenant-provisioning/provision on Identity service.
        // The X-Provisioning-Token is added by HttpIdentityProvisioningAdapter at call time
        // (read from IConfiguration["IdentityService:ProvisioningSecret"]).
        services.AddScoped<IIdentityProvisioningAdapter, HttpIdentityProvisioningAdapter>();

        // ── TENANT-B10: Documents service adapter ─────────────────────────────
        // Used by logo admin endpoints to register/deregister logos in the
        // Documents service so the anonymous /public/logo/{id} endpoint can serve them.
        services.AddHttpClient("DocumentsInternal", client =>
        {
            var docsUrl = configuration["DocumentsService:InternalUrl"] ?? "http://127.0.0.1:5006";
            client.BaseAddress = new Uri(docsUrl);
        });

        services.AddScoped<IDocumentsAdapter, HttpDocumentsAdapter>();

        return services;
    }
}
