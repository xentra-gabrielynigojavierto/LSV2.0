using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantBillingPlanRepository
{
    Task<TenantBillingPlan?> GetByIdAsync(Guid id);
    Task<TenantBillingPlan?> FindActivePlanAsync(Guid tenantId);
    Task<List<TenantBillingPlan>> GetByTenantAsync(Guid tenantId);
    Task<TenantBillingPlan> CreateAsync(TenantBillingPlan plan);
    Task UpdateAsync(TenantBillingPlan plan);
}

public interface ITenantBillingRateRepository
{
    Task<List<TenantBillingRate>> GetByPlanIdAsync(Guid billingPlanId);
    Task<TenantBillingRate?> FindRateAsync(Guid billingPlanId, string usageUnit, string? channel = null, string? providerOwnershipMode = null);
    Task<TenantBillingRate> CreateAsync(TenantBillingRate rate);
    Task UpdateAsync(TenantBillingRate rate);
    Task DeleteAsync(Guid id);
}
