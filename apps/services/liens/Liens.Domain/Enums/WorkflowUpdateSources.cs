namespace Liens.Domain.Enums;

public static class WorkflowUpdateSources
{
    public const string TenantProductSettings = "TENANT_PRODUCT_SETTINGS";
    public const string ControlCenter         = "CONTROL_CENTER";

    /// <summary>
    /// TASK-MIG-01 — indicates settings were read from or synced to the Task service.
    /// Used only in DTO responses; NOT validated by domain entity.
    /// </summary>
    public const string TaskServiceSync       = "TASK_SERVICE_SYNC";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        TenantProductSettings, ControlCenter
    };
}
