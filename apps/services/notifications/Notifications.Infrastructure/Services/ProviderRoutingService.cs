using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class ProviderRoutingService : IProviderRoutingService
{
    private readonly ITenantChannelProviderSettingRepository _settingRepo;
    private readonly ITenantProviderConfigRepository _configRepo;
    private readonly IProviderHealthRepository _healthRepo;
    private readonly ILogger<ProviderRoutingService> _logger;

    private static readonly Dictionary<string, string[]> PlatformProviderPriority = new()
    {
        ["email"] = new[] { "sendgrid", "smtp" },
        ["sms"] = new[] { "twilio" }
    };

    public ProviderRoutingService(
        ITenantChannelProviderSettingRepository settingRepo,
        ITenantProviderConfigRepository configRepo,
        IProviderHealthRepository healthRepo,
        ILogger<ProviderRoutingService> logger)
    {
        _settingRepo = settingRepo;
        _configRepo = configRepo;
        _healthRepo = healthRepo;
        _logger = logger;
    }

    public async Task<List<ProviderRoute>> ResolveRoutesAsync(Guid tenantId, string channel)
    {
        var routes = new List<ProviderRoute>();
        var setting = await _settingRepo.FindByTenantAndChannelAsync(tenantId, channel);

        if (setting?.ProviderMode == "tenant_managed")
        {
            if (setting.PrimaryTenantProviderConfigId.HasValue)
            {
                var primary = await _configRepo.FindByIdAndTenantAsync(setting.PrimaryTenantProviderConfigId.Value, tenantId);
                if (primary is { Status: "active" })
                {
                    var health = await _healthRepo.FindByProviderAsync(primary.ProviderType, channel, "tenant", primary.Id);
                    if (health == null || health.HealthStatus != "down")
                    {
                        routes.Add(new ProviderRoute
                        {
                            ProviderType = primary.ProviderType,
                            OwnershipMode = "tenant",
                            TenantProviderConfigId = primary.Id
                        });
                    }
                }
            }

            if (setting.FallbackTenantProviderConfigId.HasValue && setting.AllowAutomaticFailover)
            {
                var fallback = await _configRepo.FindByIdAndTenantAsync(setting.FallbackTenantProviderConfigId.Value, tenantId);
                if (fallback is { Status: "active" })
                {
                    routes.Add(new ProviderRoute
                    {
                        ProviderType = fallback.ProviderType,
                        OwnershipMode = "tenant",
                        TenantProviderConfigId = fallback.Id,
                        IsFailover = true
                    });
                }
            }

            if (setting.AllowPlatformFallback)
            {
                AddPlatformRoutes(channel, routes, isPlatformFallback: true);
            }
        }
        else
        {
            AddPlatformRoutes(channel, routes, isPlatformFallback: false);
        }

        _logger.LogDebug("Resolved {Count} provider routes for {TenantId} {Channel}: [{Providers}]",
            routes.Count, tenantId, channel, string.Join(", ", routes.Select(r => $"{r.OwnershipMode}:{r.ProviderType}")));

        return routes;
    }

    private static void AddPlatformRoutes(string channel, List<ProviderRoute> routes, bool isPlatformFallback)
    {
        if (!PlatformProviderPriority.TryGetValue(channel, out var providers)) return;
        var isFirst = true;
        foreach (var provider in providers)
        {
            routes.Add(new ProviderRoute
            {
                ProviderType = provider,
                OwnershipMode = "platform",
                IsFailover = !isFirst,
                IsPlatformFallback = isPlatformFallback
            });
            isFirst = false;
        }
    }
}
