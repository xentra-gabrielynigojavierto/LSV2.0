using BuildingBlocks.Authorization;
using Identity.Application.Interfaces;

namespace Identity.Infrastructure.Services;

/// <summary>
/// LS-ID-TNT-011 — Implementation of <see cref="IEffectivePermissionService"/>.
///
/// Delegates all resolution to <see cref="IEffectiveAccessService"/>, which computes
/// both product permissions (ProductRole → RolePermissionMapping) and tenant permissions
/// (system Role → RolePermissionAssignment) in a single cached call.
/// </summary>
public sealed class EffectivePermissionService : IEffectivePermissionService
{
    private readonly IEffectiveAccessService _effectiveAccess;

    public EffectivePermissionService(IEffectiveAccessService effectiveAccess)
        => _effectiveAccess = effectiveAccess;

    public async Task<bool> HasProductPermissionAsync(
        Guid userId,
        Guid tenantId,
        string productCode,
        string permissionCode,
        CancellationToken ct = default)
    {
        var access = await _effectiveAccess.GetEffectiveAccessAsync(tenantId, userId, ct);
        return access.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasTenantPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permissionCode,
        CancellationToken ct = default)
    {
        var access = await _effectiveAccess.GetEffectiveAccessAsync(tenantId, userId, ct);
        return access.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var access = await _effectiveAccess.GetEffectiveAccessAsync(tenantId, userId, ct);

        var tenantPerms = access.PermissionSources
            .Where(p => string.Equals(p.ProductCode, ProductCodes.SynqPlatform, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PermissionCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var productPerms = access.PermissionSources
            .Where(p => !string.Equals(p.ProductCode, ProductCodes.SynqPlatform, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.PermissionCode)
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                       .ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new EffectivePermissionsDto(tenantPerms, productPerms, access.Permissions);
    }
}
