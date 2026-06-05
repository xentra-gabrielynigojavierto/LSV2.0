using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IUserRoleAssignmentService
{
    Task<List<UserRoleAssignment>> GetByTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<UserRoleAssignment> AssignAsync(Guid tenantId, Guid userId, string roleCode, string? productCode = null, Guid? organizationId = null, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid tenantId, Guid userId, Guid assignmentId, Guid? actorUserId = null, CancellationToken ct = default);
}
