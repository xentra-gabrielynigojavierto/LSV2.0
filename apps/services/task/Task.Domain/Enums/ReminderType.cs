namespace Task.Domain.Enums;

public static class ReminderType
{
    public const string DueSoon = "DUE_SOON";
    public const string Overdue = "OVERDUE";
    public const string Custom  = "CUSTOM";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DueSoon, Overdue, Custom,
        };
}
