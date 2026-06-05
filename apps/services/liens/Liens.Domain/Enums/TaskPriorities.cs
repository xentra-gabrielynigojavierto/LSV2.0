namespace Liens.Domain.Enums;

public static class TaskPriorities
{
    public const string Low    = "LOW";
    public const string Medium = "MEDIUM";
    public const string High   = "HIGH";
    public const string Urgent = "URGENT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Low, Medium, High, Urgent
    };
}
