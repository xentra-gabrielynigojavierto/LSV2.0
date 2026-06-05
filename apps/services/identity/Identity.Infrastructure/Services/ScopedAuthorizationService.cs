using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

/// <summary>
/// DB-backed implementation of IScopedAuthorizationService.
/// LS-COR-AUT-007: ScopedRoleAssignment restricted to GLOBAL scope only.
/// GLOBAL scope always satisfies any authorization check.
/// </summary>
public sealed class ScopedAuthorizationService : IScopedAuthorizationService
{
    private readonly IdentityDbContext _db;

    public ScopedAuthorizationService(IdentityDbContext db) => _db = db;

    public Task<bool> HasOrganizationRoleAsync(
        Guid   userId,
        string roleName,
        Guid   organizationId,
        CancellationToken ct = default)
        => _db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId   == userId &&
                s.IsActive &&
                s.Role.Name == roleName &&
                s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global,
                ct);

    public Task<bool> HasProductRoleAsync(
        Guid   userId,
        string roleName,
        Guid   productId,
        CancellationToken ct = default)
        => _db.ScopedRoleAssignments
            .AnyAsync(s =>
                s.UserId   == userId &&
                s.IsActive &&
                s.Role.Name == roleName &&
                s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global,
                ct);

    public async Task<ScopedRoleSummaryResponse> GetScopedRoleSummaryAsync(
        Guid   userId,
        CancellationToken ct = default)
    {
        var rows = await _db.ScopedRoleAssignments
            .Include(s => s.Role)
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderBy(s => s.AssignedAtUtc)
            .ToListAsync(ct);

        var entries = rows
            .Select(s => new ScopedRoleEntry(
                s.Id,
                s.Role.Name,
                s.ScopeType,
                s.OrganizationId,
                s.ProductId,
                s.OrganizationRelationshipId,
                s.TenantId))
            .ToList();

        return new ScopedRoleSummaryResponse(userId, entries.Count, entries);
    }
}
