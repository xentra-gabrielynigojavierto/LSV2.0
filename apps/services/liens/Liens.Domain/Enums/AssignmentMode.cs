namespace Liens.Domain.Enums;

public static class AssignmentMode
{
    public const string UseTemplateDefault = "USE_TEMPLATE_DEFAULT";
    public const string LeaveUnassigned    = "LEAVE_UNASSIGNED";
    public const string AssignEventActor   = "ASSIGN_EVENT_ACTOR";
    public const string AssignByRole       = "ASSIGN_BY_ROLE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        UseTemplateDefault,
        LeaveUnassigned,
        AssignEventActor,
        AssignByRole,
    };

    public const string Default = UseTemplateDefault;
}
