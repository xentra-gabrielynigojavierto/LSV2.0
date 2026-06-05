namespace Comms.Domain.Enums;

public static class MessageStatus
{
    public const string Created = "Created";
    public const string Posted = "Posted";

    public static readonly IReadOnlyList<string> All = new[] { Created, Posted };
}
