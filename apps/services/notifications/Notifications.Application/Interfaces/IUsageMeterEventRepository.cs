using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IUsageMeterEventRepository
{
    Task CreateSilentAsync(UsageMeterEvent evt);
    Task<int> CountSinceAsync(Guid tenantId, string usageUnit, DateTime since, string? channel = null);
    Task<int> CountSinceMultipleAsync(Guid tenantId, string[] usageUnits, DateTime since, string? channel = null);
}
