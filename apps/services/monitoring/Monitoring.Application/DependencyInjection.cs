using Microsoft.Extensions.DependencyInjection;
using Monitoring.Application.Scheduling;

namespace Monitoring.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Default cycle executor is a no-op. Infrastructure overrides this
        // with the real registry-driven executor (entity load + per-entity
        // hook iteration). The Application-layer default ensures
        // resolution still works in test/host setups that wire only
        // Application.
        services.AddScoped<IMonitoringCycleExecutor, NoopMonitoringCycleExecutor>();

        // Default per-entity executor is a no-op. Later features (real
        // health/HTTP/DB checks) will replace this registration via DI.
        services.AddScoped<IMonitoredEntityExecutor, NoopMonitoredEntityExecutor>();

        return services;
    }
}
