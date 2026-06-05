namespace Comms.Domain.Enums;

public static class WaitingState
{
    public const string None = "None";
    public const string WaitingInternal = "WaitingInternal";
    public const string WaitingExternal = "WaitingExternal";

    public static readonly IReadOnlyList<string> All = new[]
    {
        None, WaitingInternal, WaitingExternal
    };
}
