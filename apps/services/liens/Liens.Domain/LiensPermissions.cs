namespace Liens.Domain;

public static class LiensPermissions
{
    public const string ProductCode = "SYNQ_LIENS";

    public const string LienRead     = "SYNQ_LIENS.lien:read";
    public const string LienCreate   = "SYNQ_LIENS.lien:create";
    public const string LienUpdate   = "SYNQ_LIENS.lien:update";
    public const string LienOffer    = "SYNQ_LIENS.lien:offer";
    public const string LienReadOwn  = "SYNQ_LIENS.lien:read:own";
    public const string LienBrowse   = "SYNQ_LIENS.lien:browse";
    public const string LienPurchase = "SYNQ_LIENS.lien:purchase";
    public const string LienReadHeld = "SYNQ_LIENS.lien:read:held";
    public const string LienService  = "SYNQ_LIENS.lien:service";
    public const string LienSettle   = "SYNQ_LIENS.lien:settle";

    public const string CaseRead   = "SYNQ_LIENS.case:read";
    public const string CaseCreate = "SYNQ_LIENS.case:create";
    public const string CaseUpdate = "SYNQ_LIENS.case:update";

    public const string TaskRead    = "SYNQ_LIENS.task:read";
    public const string TaskCreate  = "SYNQ_LIENS.task:create";
    public const string TaskEditOwn = "SYNQ_LIENS.task:edit:own";
    public const string TaskEditAll = "SYNQ_LIENS.task:edit:all";
    public const string TaskAssign  = "SYNQ_LIENS.task:assign";
    public const string TaskComplete = "SYNQ_LIENS.task:complete";
    public const string TaskCancel  = "SYNQ_LIENS.task:cancel";

    public const string WorkflowManage = "SYNQ_LIENS.workflow:manage";

    public const string TaskTemplateManage   = "SYNQ_LIENS.task_template:manage";
    public const string TaskAutomationManage = "SYNQ_LIENS.task_automation:manage";
    public const string TaskNoteManage       = "SYNQ_LIENS.task_note:manage";

    public const string CaseNoteManage = "SYNQ_LIENS.case_note:manage";
}
