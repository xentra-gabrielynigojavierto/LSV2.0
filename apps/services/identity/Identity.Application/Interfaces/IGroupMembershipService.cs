using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IGroupMembershipService
{
    Task<AccessGroupMembership> AddMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId = null, CancellationToken ct = default);
    Task<List<AccessGroupMembership>> ListMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task<List<AccessGroupMembership>> ListGroupsForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
