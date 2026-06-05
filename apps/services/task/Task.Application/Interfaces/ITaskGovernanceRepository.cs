using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskGovernanceRepository
{
    System.Threading.Tasks.Task<TaskGovernanceSettings?> GetByTenantAndProductAsync(Guid tenantId, string? sourceProductCode, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskGovernanceSettings?> GetTenantDefaultAsync(Guid tenantId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskGovernanceSettings settings, CancellationToken ct = default);
}
