namespace Liens.Domain.Enums;

public static class BillOfSaleStatus
{
    public const string Draft     = "Draft";
    public const string Pending   = "Pending";
    public const string Executed  = "Executed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft, Pending, Executed, Cancelled
    };

    public static readonly IReadOnlySet<string> Open = new HashSet<string>
    {
        Draft, Pending
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Executed, Cancelled
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Draft]     = new HashSet<string> { Pending, Cancelled },
            [Pending]   = new HashSet<string> { Executed, Cancelled },
            [Executed]  = new HashSet<string>(),
            [Cancelled] = new HashSet<string>(),
        };
}
