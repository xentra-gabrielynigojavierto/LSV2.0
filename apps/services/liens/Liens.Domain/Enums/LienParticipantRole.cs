namespace Liens.Domain.Enums;

public static class LienParticipantRole
{
    public const string Seller = "Seller";
    public const string Buyer  = "Buyer";
    public const string Holder = "Holder";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Seller, Buyer, Holder
    };
}
