namespace Identity.Domain;

public static class ProviderModes
{
    public const string Sell = "sell";
    public const string Manage = "manage";

    private static readonly HashSet<string> ValidModes = new(StringComparer.OrdinalIgnoreCase) { Sell, Manage };

    public static bool IsValid(string? mode) => mode is not null && ValidModes.Contains(mode);

    public static string Normalize(string? mode) =>
        mode is not null && ValidModes.Contains(mode)
            ? mode.ToLowerInvariant()
            : Sell;

    public static bool IsSellMode(string? mode) =>
        string.Equals(Normalize(mode), Sell, StringComparison.OrdinalIgnoreCase);

    public static bool IsManageMode(string? mode) =>
        string.Equals(Normalize(mode), Manage, StringComparison.OrdinalIgnoreCase);
}
