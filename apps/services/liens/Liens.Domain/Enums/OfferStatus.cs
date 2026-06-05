namespace Liens.Domain.Enums;

public static class OfferStatus
{
    public const string Pending   = "Pending";
    public const string Accepted  = "Accepted";
    public const string Rejected  = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string Expired   = "Expired";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending, Accepted, Rejected, Withdrawn, Expired
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Accepted, Rejected, Withdrawn, Expired
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Pending]   = new HashSet<string> { Accepted, Rejected, Withdrawn, Expired },
            [Accepted]  = new HashSet<string>(),
            [Rejected]  = new HashSet<string>(),
            [Withdrawn] = new HashSet<string>(),
            [Expired]   = new HashSet<string>(),
        };
}
