namespace Liens.Domain.Enums;

public static class CaseNoteCategory
{
    public const string General  = "general";
    public const string Internal = "internal";
    public const string FollowUp = "follow-up";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            General,
            Internal,
            FollowUp,
        };
}
