namespace Liens.Domain.Enums;

public static class TaskGenerationEventType
{
    public const string CaseCreated                = "CASE_CREATED";
    public const string LienCreated                = "LIEN_CREATED";
    public const string CaseWorkflowStageChanged   = "CASE_WORKFLOW_STAGE_CHANGED";
    public const string LienWorkflowStageChanged   = "LIEN_WORKFLOW_STAGE_CHANGED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        CaseCreated,
        LienCreated,
        CaseWorkflowStageChanged,
        LienWorkflowStageChanged,
    };

    public static readonly IReadOnlySet<string> StageEvents = new HashSet<string>
    {
        CaseWorkflowStageChanged,
        LienWorkflowStageChanged,
    };
}
