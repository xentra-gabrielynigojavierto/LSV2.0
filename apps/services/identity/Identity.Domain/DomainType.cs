namespace Identity.Domain;

public static class DomainType
{
    public const string Subdomain = "SUBDOMAIN";
    public const string Custom    = "CUSTOM";

    public static readonly IReadOnlyList<string> All = [Subdomain, Custom];

    public static bool IsValid(string value) => All.Contains(value);
}
