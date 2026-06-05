namespace Liens.Domain.Enums;

public static class LienStatus
{
    public const string Draft      = "Draft";
    public const string Offered    = "Offered";
    public const string UnderReview = "UnderReview";
    public const string Sold       = "Sold";
    public const string Active     = "Active";
    public const string Settled    = "Settled";
    public const string Withdrawn  = "Withdrawn";
    public const string Cancelled  = "Cancelled";
    public const string Disputed   = "Disputed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed
    };

    public static readonly IReadOnlySet<string> Open = new HashSet<string>
    {
        Draft, Offered, UnderReview, Sold, Active, Disputed
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Settled, Withdrawn, Cancelled
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Draft]       = new HashSet<string> { Offered, Cancelled },
            [Offered]     = new HashSet<string> { UnderReview, Sold, Withdrawn },
            [UnderReview] = new HashSet<string> { Sold, Withdrawn },
            [Sold]        = new HashSet<string> { Active, Cancelled },
            [Active]      = new HashSet<string> { Settled, Disputed, Cancelled },
            [Disputed]    = new HashSet<string> { Active, Settled, Cancelled },
            [Settled]     = new HashSet<string>(),
            [Withdrawn]   = new HashSet<string>(),
            [Cancelled]   = new HashSet<string>(),
        };
}
