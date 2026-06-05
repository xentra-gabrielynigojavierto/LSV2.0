namespace Comms.Domain.Enums;

public static class ContextType
{
    public const string Case = "Case";
    public const string Lien = "Lien";
    public const string Referral = "Referral";
    public const string FundingRequest = "FundingRequest";
    public const string Bill = "Bill";
    public const string Payout = "Payout";
    public const string General = "General";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Case, Lien, Referral, FundingRequest, Bill, Payout, General
    };
}
