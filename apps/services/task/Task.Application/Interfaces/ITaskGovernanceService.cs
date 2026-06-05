using Task.Application.DTOs;
using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskGovernanceService
{
    /// <summary>
    /// Resolves effective governance settings using the priority order:
    /// 1. Product-level settings (if sourceProductCode provided and a record exists)
    /// 2. Tenant-level default settings
    /// 3. Hard-coded fallback defaults
    /// </summary>
    System.Threading.Tasks.Task<ResolvedGovernance> ResolveAsync(Guid tenantId, string? sourceProductCode, CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskGovernanceDto> UpsertAsync(Guid tenantId, Guid userId, UpsertTaskGovernanceRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskGovernanceDto?> GetAsync(Guid tenantId, string? sourceProductCode, CancellationToken ct = default);
}
