namespace Liens.Domain.Enums;

public static class DuplicatePreventionMode
{
    public const string None                          = "NONE";
    public const string SameRuleSameEntityOpenTask    = "SAME_RULE_SAME_ENTITY_OPEN_TASK";
    public const string SameTemplateSameEntityOpenTask = "SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        None,
        SameRuleSameEntityOpenTask,
        SameTemplateSameEntityOpenTask,
    };

    public const string Default = SameRuleSameEntityOpenTask;
}
