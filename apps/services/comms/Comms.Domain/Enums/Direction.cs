namespace Comms.Domain.Enums;

public static class Direction
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
    public const string Internal = "Internal";

    public static readonly IReadOnlyList<string> All = new[] { Inbound, Outbound, Internal };
}
