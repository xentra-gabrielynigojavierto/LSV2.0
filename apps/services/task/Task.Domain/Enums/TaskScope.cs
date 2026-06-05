namespace Task.Domain.Enums;

public static class TaskScope
{
    public const string General = "GENERAL";
    public const string Product = "PRODUCT";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            General, Product,
        };
}
