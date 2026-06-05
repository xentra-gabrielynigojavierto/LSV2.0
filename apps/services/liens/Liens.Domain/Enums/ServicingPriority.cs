namespace Liens.Domain.Enums;

public static class ServicingPriority
{
    public const string Low    = "Low";
    public const string Normal = "Normal";
    public const string High   = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Low, Normal, High, Urgent
    };
}
