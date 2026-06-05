namespace Identity.Domain;

public static class OrgType
{
    public const string LawFirm  = "LAW_FIRM";
    public const string Provider = "PROVIDER";
    public const string Funder   = "FUNDER";
    public const string LienOwner = "LIEN_OWNER";
    public const string Internal = "INTERNAL";

    public static readonly IReadOnlyList<string> All =
        [LawFirm, Provider, Funder, LienOwner, Internal];

    public static bool IsValid(string value) => All.Contains(value);
}
