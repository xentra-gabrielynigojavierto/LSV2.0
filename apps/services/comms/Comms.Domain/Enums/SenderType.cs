namespace Comms.Domain.Enums;

public static class SenderType
{
    public const string NoReply = "NOREPLY";
    public const string Support = "SUPPORT";
    public const string Operations = "OPERATIONS";
    public const string Product = "PRODUCT";
    public const string Custom = "CUSTOM";

    public static readonly IReadOnlyList<string> All = new[]
    {
        NoReply, Support, Operations, Product, Custom
    };

    public static bool IsValid(string type) =>
        All.Contains(type, StringComparer.OrdinalIgnoreCase);
}
