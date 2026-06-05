namespace Liens.Domain.Enums;

public static class StartStageMode
{
    public const string FirstActiveStage = "FIRST_ACTIVE_STAGE";
    public const string ExplicitStage    = "EXPLICIT_STAGE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        FirstActiveStage,
        ExplicitStage,
    };
}
