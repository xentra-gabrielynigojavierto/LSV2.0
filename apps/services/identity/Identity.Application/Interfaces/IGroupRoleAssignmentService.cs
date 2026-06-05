using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IGroupRoleAssignmentService
{
    Task<GroupRoleAssignment> AssignAsync(Guid tenantId, Guid groupId, string roleCode, string? productCode = null, Guid? organizationId = null, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid tenantId, Guid groupId, Guid assignmentId, Guid? actorUserId = null, CancellationToken ct = default);
    Task<List<GroupRoleAssignment>> ListAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
}
