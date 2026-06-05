using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IGroupProductAccessService
{
    Task<GroupProductAccess> GrantAsync(Guid tenantId, Guid groupId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid tenantId, Guid groupId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
    Task<List<GroupProductAccess>> ListAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
}
