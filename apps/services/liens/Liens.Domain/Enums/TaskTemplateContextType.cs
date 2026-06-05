namespace Liens.Domain.Enums;

public static class TaskTemplateContextType
{
    public const string General = "GENERAL";
    public const string Case    = "CASE";
    public const string Lien    = "LIEN";
    public const string Stage   = "STAGE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        General, Case, Lien, Stage
    };
}
