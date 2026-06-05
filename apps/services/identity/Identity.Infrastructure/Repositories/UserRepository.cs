using Identity.Application;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default) =>
        _db.Users
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default) =>
        _db.Users
            // Phase G: ScopedRoleAssignments is the sole authoritative role source.
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Users
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
                .ThenInclude(s => s.Role)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);

    public async Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);

        foreach (var roleId in roleIds)
        {
            // Phase G: single write — ScopedRoleAssignment only.
            // UserRoles table dropped by migration 20260330200004.
            var scoped = ScopedRoleAssignment.Create(
                userId:    user.Id,
                roleId:    roleId,
                scopeType: ScopedRoleAssignment.ScopeTypes.Global);

            await _db.ScopedRoleAssignments.AddAsync(scoped, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAvatarAsync(Guid userId, Guid? avatarDocumentId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (avatarDocumentId.HasValue)
            user.SetAvatar(avatarDocumentId.Value);
        else
            user.ClearAvatar();

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdatePhoneAsync(Guid userId, string? phone, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var changed = user.SetPhone(phone);
        if (changed) await _db.SaveChangesAsync(ct);
        return changed;
    }

    public Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(
        Guid userId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            // Chain 1: products → roles → Phase 3 org-type eligibility rules
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationProducts)
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.ProductRoles)
                            .ThenInclude(pr => pr.OrgTypeRules)
                                .ThenInclude(r => r.OrganizationType)
            // Chain 2: Phase 1 — canonical OrganizationType catalog record on the org itself
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationTypeRef)
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<List<UserOrganizationMembership>> GetActiveMembershipsWithProductsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default) =>
        _db.UserOrganizationMemberships
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationProducts.Where(op => op.IsEnabled))
                    .ThenInclude(op => op.Product)
                        .ThenInclude(p => p.ProductRoles.Where(pr => pr.IsActive))
                            .ThenInclude(pr => pr.OrgTypeRules)
                                .ThenInclude(r => r.OrganizationType)
            .Include(m => m.Organization)
                .ThenInclude(o => o.OrganizationTypeRef)
            .Where(m => m.UserId == userId
                     && m.IsActive
                     && m.Organization.IsActive
                     && m.Organization.TenantId == tenantId)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(ct);

    /// <summary>
    /// UIX-003-03: Persists pending change-tracked mutations for User entities
    /// already loaded by this repository (e.g. RecordLogin, IncrementSessionVersion).
    /// </summary>
    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}
