namespace Liens.Domain.Enums;

public static class TaskSourceType
{
    public const string Manual          = "MANUAL";
    public const string SystemGenerated = "SYSTEM_GENERATED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Manual,
        SystemGenerated,
    };
}
