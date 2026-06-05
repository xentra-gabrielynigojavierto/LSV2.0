using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IGroupService
{
    Task<AccessGroup> CreateAsync(Guid tenantId, string name, string? description, GroupScopeType scopeType, string? productCode, Guid? organizationId, Guid? actorUserId = null, CancellationToken ct = default);
    Task<AccessGroup> UpdateAsync(Guid tenantId, Guid groupId, string name, string? description, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid tenantId, Guid groupId, Guid? actorUserId = null, CancellationToken ct = default);
    Task<AccessGroup?> GetByIdAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task<List<AccessGroup>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
