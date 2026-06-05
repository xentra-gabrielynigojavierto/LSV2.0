using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default);
    Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
    Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(Guid userId, CancellationToken ct = default);

    Task<List<UserOrganizationMembership>> GetActiveMembershipsWithProductsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's AvatarDocumentId. Pass null to clear the avatar.
    /// </summary>
    Task UpdateAvatarAsync(Guid userId, Guid? avatarDocumentId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's primary phone number. Pass null or whitespace to clear it.
    /// Callers are expected to have already validated the value against E.164 format.
    /// Returns true when the value actually changed, false when it was already equal.
    /// Throws InvalidOperationException when the user is not found.
    /// </summary>
    Task<bool> UpdatePhoneAsync(Guid userId, string? phone, CancellationToken ct = default);

    /// <summary>
    /// UIX-003-03: Persists pending EF change-tracked mutations on the Users entity
    /// (e.g. RecordLogin, IncrementSessionVersion). Callers must load the entity via
    /// this repository before mutating — do not use this for unrelated entities.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
