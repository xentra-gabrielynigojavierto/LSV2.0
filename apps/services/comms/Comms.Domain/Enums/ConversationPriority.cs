namespace Comms.Domain.Enums;

public static class ConversationPriority
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Low, Normal, High, Urgent
    };
}
