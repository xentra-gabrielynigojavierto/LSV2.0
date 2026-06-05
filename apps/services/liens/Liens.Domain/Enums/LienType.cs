namespace Liens.Domain.Enums;

public static class LienType
{
    public const string MedicalLien       = "MedicalLien";
    public const string AttorneyLien      = "AttorneyLien";
    public const string SettlementAdvance = "SettlementAdvance";
    public const string WorkersCompLien   = "WorkersCompLien";
    public const string PropertyLien      = "PropertyLien";
    public const string Other             = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        MedicalLien, AttorneyLien, SettlementAdvance, WorkersCompLien, PropertyLien, Other
    };
}
