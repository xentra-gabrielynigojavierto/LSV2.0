using BuildingBlocks.Context;

namespace BuildingBlocks.Authorization;

public sealed class AuthorizationService
{
    private readonly IPermissionService _perms;

    public AuthorizationService(IPermissionService perms) => _perms = perms;

    public async Task<bool> IsAuthorizedAsync(
        ICurrentRequestContext ctx,
        string permissionCode,
        CancellationToken ct = default)
    {
        if (ctx.IsPlatformAdmin) return true;
        return await _perms.HasPermissionAsync(ctx.ProductRoles, permissionCode, ct);
    }

    public async Task RequirePermissionAsync(
        ICurrentRequestContext ctx,
        string permissionCode,
        CancellationToken ct = default)
    {
        if (!await IsAuthorizedAsync(ctx, permissionCode, ct))
            throw new global::BuildingBlocks.Exceptions.ForbiddenException(permissionCode);
    }
}
