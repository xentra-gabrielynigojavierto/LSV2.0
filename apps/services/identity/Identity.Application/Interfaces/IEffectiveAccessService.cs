namespace Identity.Application.Interfaces;

public record EffectiveProductEntry(string ProductCode, string Source, Guid? GroupId = null, string? GroupName = null);
public record EffectiveRoleEntry(string RoleCode, string? ProductCode, string Source, Guid? GroupId = null, string? GroupName = null);
public record EffectivePermissionEntry(string PermissionCode, string ProductCode, string Source, string? ViaRoleCode = null, Guid? GroupId = null, string? GroupName = null);

public record EffectiveAccessResult(
    List<string> Products,
    Dictionary<string, List<string>> ProductRoles,
    List<string> ProductRolesFlat,
    List<string> TenantRoles,
    List<EffectiveProductEntry> ProductSources,
    List<EffectiveRoleEntry> RoleSources,
    List<string> Permissions,
    List<EffectivePermissionEntry> PermissionSources);

public interface IEffectiveAccessService
{
    Task<EffectiveAccessResult> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
