using BuildingBlocks.Authorization;

namespace CareConnect.Infrastructure.Services;

public sealed class CareConnectPermissionService : IPermissionService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductRoleCodes.CareConnectReferrer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.ReferralCreate,
                PermissionCodes.ReferralReadOwn,
                PermissionCodes.ReferralCancel,
                PermissionCodes.ProviderSearch,
                PermissionCodes.ProviderMap,
                PermissionCodes.AppointmentCreate,
                PermissionCodes.AppointmentReadOwn,
                PermissionCodes.DashboardRead,
            },
            [ProductRoleCodes.CareConnectReceiver] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionCodes.ReferralReadAddressed,
                PermissionCodes.ReferralAccept,
                PermissionCodes.ReferralDecline,
                PermissionCodes.AppointmentCreate,
                PermissionCodes.AppointmentUpdate,
                PermissionCodes.AppointmentManage,
                PermissionCodes.AppointmentReadOwn,
                PermissionCodes.ScheduleManage,
                PermissionCodes.ProviderSearch,
                PermissionCodes.ProviderMap,
                PermissionCodes.DashboardRead,
            },
        };

    public Task<bool> HasPermissionAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string permissionCode,
        CancellationToken ct = default)
    {
        foreach (var roleCode in productRoleCodes)
        {
            if (RolePermissions.TryGetValue(roleCode, out var perms) && perms.Contains(permissionCode))
                return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleCode in productRoleCodes)
        {
            if (RolePermissions.TryGetValue(roleCode, out var perms))
                result.UnionWith(perms);
        }
        return Task.FromResult<IReadOnlySet<string>>(result);
    }
}
