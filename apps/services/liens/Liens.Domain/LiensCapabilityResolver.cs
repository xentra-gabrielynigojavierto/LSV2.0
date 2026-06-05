namespace Liens.Domain;

public static class LiensCapabilityResolver
{
    private static readonly string[] SellPermissions =
    {
        LiensPermissions.LienCreate,
        LiensPermissions.LienOffer,
        LiensPermissions.LienReadOwn,
    };

    private static readonly string[] ManageInternalPermissions =
    {
        LiensPermissions.LienReadHeld,
        LiensPermissions.LienService,
        LiensPermissions.LienSettle,
    };

    public static bool HasCapability(IReadOnlyCollection<string> permissions, string capability)
    {
        return capability switch
        {
            LiensCapabilities.Sell => HasAll(permissions, SellPermissions),
            LiensCapabilities.ManageInternal => HasAll(permissions, ManageInternalPermissions),
            _ => false,
        };
    }

    public static IReadOnlyList<string> ResolveAll(IReadOnlyCollection<string> permissions)
    {
        var caps = new List<string>(2);
        if (HasAll(permissions, SellPermissions)) caps.Add(LiensCapabilities.Sell);
        if (HasAll(permissions, ManageInternalPermissions)) caps.Add(LiensCapabilities.ManageInternal);
        return caps;
    }

    private static bool HasAll(IReadOnlyCollection<string> permissions, string[] required)
    {
        foreach (var r in required)
        {
            bool found = false;
            foreach (var p in permissions)
            {
                if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }
}
