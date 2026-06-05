using Comms.Domain.Enums;

namespace Comms.Domain.Constants;

public static class SlaDefaults
{
    public static readonly IReadOnlyDictionary<string, (TimeSpan FirstResponse, TimeSpan Resolution)> Durations =
        new Dictionary<string, (TimeSpan, TimeSpan)>
        {
            { ConversationPriority.Low, (TimeSpan.FromHours(24), TimeSpan.FromHours(120)) },
            { ConversationPriority.Normal, (TimeSpan.FromHours(8), TimeSpan.FromHours(72)) },
            { ConversationPriority.High, (TimeSpan.FromHours(4), TimeSpan.FromHours(24)) },
            { ConversationPriority.Urgent, (TimeSpan.FromHours(1), TimeSpan.FromHours(8)) },
        };

    public static (TimeSpan FirstResponse, TimeSpan Resolution) GetDurations(string priority)
    {
        if (Durations.TryGetValue(priority, out var durations))
            return durations;
        return Durations[ConversationPriority.Normal];
    }
}
