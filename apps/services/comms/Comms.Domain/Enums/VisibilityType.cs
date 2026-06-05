namespace Comms.Domain.Enums;

public static class VisibilityType
{
    public const string InternalOnly = "InternalOnly";
    public const string SharedExternal = "SharedExternal";

    public static readonly IReadOnlyList<string> All = new[] { InternalOnly, SharedExternal };
}
