using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IUserProductAccessService
{
    Task<List<UserProductAccess>> GetByTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<UserProductAccess?> GetByTenantUserAndCodeAsync(Guid tenantId, Guid userId, string productCode, CancellationToken ct = default);
    Task<UserProductAccess> GrantAsync(Guid tenantId, Guid userId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid tenantId, Guid userId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
}
