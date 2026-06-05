namespace Comms.Domain.Enums;

public static class EmailDirection
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";

    public static readonly IReadOnlyList<string> All = new[] { Inbound, Outbound };
}
