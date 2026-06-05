using Identity.Domain;

namespace Identity.Application.Interfaces;

public record AccessSourceSnapshot(
    List<TenantProductEntitlement> TenantProducts,
    List<UserProductAccess> UserProducts,
    List<UserRoleAssignment> UserRoles);

public interface IAccessSourceQueryService
{
    Task<AccessSourceSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
