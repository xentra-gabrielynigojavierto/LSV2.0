namespace BuildingBlocks.Authorization;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string permissionCode,
        CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetPermissionsAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default);
}
