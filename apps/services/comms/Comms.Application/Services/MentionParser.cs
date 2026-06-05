using System.Text.RegularExpressions;

namespace Comms.Application.Services;

public static partial class MentionParser
{
    public const int MaxMentionsPerMessage = 10;

    private static readonly Regex MentionRegex = new(
        @"@\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static List<Guid> ExtractMentionedUserIds(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        var matches = MentionRegex.Matches(body);
        var ids = new HashSet<Guid>();

        foreach (Match match in matches)
        {
            if (ids.Count >= MaxMentionsPerMessage)
                break;

            if (Guid.TryParse(match.Groups[1].Value, out var userId))
                ids.Add(userId);
        }

        return ids.ToList();
    }
}
