namespace Liens.Domain.Enums;

public static class ContactType
{
    public const string LawFirm      = "LawFirm";
    public const string Provider     = "Provider";
    public const string LienHolder   = "LienHolder";
    public const string CaseManager  = "CaseManager";
    public const string InternalUser = "InternalUser";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        LawFirm, Provider, LienHolder, CaseManager, InternalUser
    };
}
