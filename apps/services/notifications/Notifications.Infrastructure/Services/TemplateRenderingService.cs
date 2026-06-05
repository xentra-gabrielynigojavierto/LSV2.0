using System.Text.RegularExpressions;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public partial class TemplateRenderingService : ITemplateRenderingService
{
    [GeneratedRegex(@"\{\{(\w[\w.]*)\}\}")]
    private static partial Regex MustachePattern();

    public RenderResult Render(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data)
    {
        return new RenderResult
        {
            Subject = subjectTemplate != null ? ReplaceTokens(subjectTemplate, data) : null,
            Body = ReplaceTokens(bodyTemplate, data),
            Text = textTemplate != null ? ReplaceTokens(textTemplate, data) : null
        };
    }

    public RenderResult RenderBranded(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data, Dictionary<string, string> brandingTokens)
    {
        var merged = new Dictionary<string, string>(brandingTokens);
        foreach (var kv in data)
        {
            if (!kv.Key.StartsWith("brand.", StringComparison.OrdinalIgnoreCase))
                merged[kv.Key] = kv.Value;
        }

        return Render(subjectTemplate, bodyTemplate, textTemplate, merged);
    }

    private static string ReplaceTokens(string template, Dictionary<string, string> data)
    {
        return MustachePattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return data.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
