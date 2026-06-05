namespace Liens.Domain.Enums;

public static class CaseStatus
{
    public const string PreDemand     = "PreDemand";
    public const string DemandSent    = "DemandSent";
    public const string InNegotiation = "InNegotiation";
    public const string CaseSettled   = "CaseSettled";
    public const string Closed        = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        PreDemand, DemandSent, InNegotiation, CaseSettled, Closed
    };
}
