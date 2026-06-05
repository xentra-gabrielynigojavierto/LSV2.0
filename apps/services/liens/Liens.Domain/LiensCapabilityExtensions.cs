using BuildingBlocks.Context;

namespace Liens.Domain;

public static class LiensCapabilityExtensions
{
    public static bool HasCapability(this ICurrentRequestContext context, string capability) =>
        IsAdminBypass(context) || LiensCapabilityResolver.HasCapability(context.Permissions, capability);

    public static bool CanSellLiens(this ICurrentRequestContext context) =>
        context.HasCapability(LiensCapabilities.Sell);

    public static bool CanManageLiensInternal(this ICurrentRequestContext context) =>
        context.HasCapability(LiensCapabilities.ManageInternal);

    public static IReadOnlyList<string> GetLiensCapabilities(this ICurrentRequestContext context)
    {
        if (IsAdminBypass(context))
            return [LiensCapabilities.Sell, LiensCapabilities.ManageInternal];

        return LiensCapabilityResolver.ResolveAll(context.Permissions);
    }

    private static bool IsAdminBypass(ICurrentRequestContext context) =>
        context.IsPlatformAdmin ||
        context.Roles.Contains(BuildingBlocks.Authorization.Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);
}
