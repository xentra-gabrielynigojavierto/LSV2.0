namespace Task.Domain.Enums;

public static class TaskPriority
{
    public const string Low    = "LOW";
    public const string Medium = "MEDIUM";
    public const string High   = "HIGH";
    public const string Urgent = "URGENT";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Low, Medium, High, Urgent,
        };
}
