namespace Liens.Domain.Enums;

public static class DueDateMode
{
    public const string UseTemplateDefault  = "USE_TEMPLATE_DEFAULT";
    public const string OverrideOffsetDays  = "OVERRIDE_OFFSET_DAYS";
    public const string NoDueDate           = "NO_DUE_DATE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        UseTemplateDefault,
        OverrideOffsetDays,
        NoDueDate,
    };

    public const string Default = UseTemplateDefault;
}
