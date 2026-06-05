namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: A single governance change grouped inside a release package.
/// EntitySnapshotJson is safe — must not contain secrets, credentials, or phone numbers.
/// </summary>
public class SmsGovernanceReleaseItem
{
    public Guid   Id                  { get; set; }
    public Guid   ReleasePackageId    { get; set; }

    /// <summary>rule_pack | rule | compliance_profile | policy | template</summary>
    public string EntityType          { get; set; } = string.Empty;
    public Guid   EntityId            { get; set; }
    public int?   EntityVersionNumber { get; set; }

    /// <summary>create | update | disable | rollback | import | activate</summary>
    public string  ActionType         { get; set; } = string.Empty;

    /// <summary>Safe JSON snapshot — no secrets, no phone numbers, no credentials.</summary>
    public string? EntitySnapshotJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public string?  CreatedBy { get; set; }
}

public static class ReleaseEntityTypes
{
    public const string RulePack          = "rule_pack";
    public const string Rule              = "rule";
    public const string ComplianceProfile = "compliance_profile";
    public const string Policy            = "policy";
    public const string Template          = "template";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { RulePack, Rule, ComplianceProfile, Policy, Template };
}

public static class ReleaseActionTypes
{
    public const string Create   = "create";
    public const string Update   = "update";
    public const string Disable  = "disable";
    public const string Rollback = "rollback";
    public const string Import   = "import";
    public const string Activate = "activate";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Create, Update, Disable, Rollback, Import, Activate };
}
