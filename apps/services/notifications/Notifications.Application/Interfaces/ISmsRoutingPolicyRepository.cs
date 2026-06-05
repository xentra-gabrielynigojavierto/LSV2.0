using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ISmsRoutingPolicyRepository
{
    Task<SmsRoutingPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<SmsRoutingPolicy> Items, int Total)> ListAsync(SmsRoutingPolicyQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<SmsRoutingPolicy>> GetActiveForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<SmsRoutingPolicy> CreateAsync(SmsRoutingPolicy policy, CancellationToken ct = default);
    Task UpdateAsync(SmsRoutingPolicy policy, CancellationToken ct = default);
}
