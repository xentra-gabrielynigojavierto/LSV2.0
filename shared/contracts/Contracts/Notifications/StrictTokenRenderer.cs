using System.Text.RegularExpressions;

namespace Contracts.Notifications;

/// <summary>
/// Deterministic, channel-neutral Mustache-style token renderer for the
/// platform notification contract (E12.1).
///
/// <para>
/// Reuses the same <c>{{token}}</c> grammar as the existing notifications
/// service template renderer so producers can move between the two without
/// authoring two template dialects. The strict variant fails explicitly
/// when a required token is missing or when the template key is unknown,
/// so a malformed envelope does not silently produce a notification with
/// raw <c>{{tokens}}</c> in the body.
/// </para>
/// </summary>
public static partial class StrictTokenRenderer
{
    [GeneratedRegex(@"\{\{(\w[\w.]*)\}\}")]
    private static partial Regex TokenPattern();

    public sealed record RenderedTemplate(string? Subject, string Body);

    /// <summary>
    /// Resolve <paramref name="envelope"/> against the registry and render
    /// the template's subject + body deterministically.
    /// </summary>
    /// <exception cref="UnknownTemplateException">
    /// When <paramref name="envelope"/>.TemplateKey is not in <paramref name="registry"/>.
    /// </exception>
    /// <exception cref="MissingRequiredTokenException">
    /// When one or more required tokens declared by the template are absent
    /// from <paramref name="envelope"/>.BodyVariables.
    /// </exception>
    public static RenderedTemplate Render(NotificationEnvelope envelope, NotificationTemplateRegistry registry)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));
        if (registry is null) throw new ArgumentNullException(nameof(registry));

        if (!registry.TryGet(envelope.TemplateKey, out var template))
        {
            throw new UnknownTemplateException(envelope.TemplateKey);
        }

        return Render(template, envelope.BodyVariables);
    }

    /// <summary>
    /// Render an explicit template + variables pair (used by tests and by
    /// the notifications service when overrides have already been resolved).
    /// </summary>
    public static RenderedTemplate Render(
        NotificationTemplate template,
        IReadOnlyDictionary<string, string?>? variables)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));

        var data = variables ?? new Dictionary<string, string?>();
        var missing = template.Tokens
            .Where(t => t.Required && !HasValue(data, t.Name))
            .Select(t => t.Name)
            .ToList();
        if (missing.Count > 0)
        {
            throw new MissingRequiredTokenException(template.Key, missing);
        }

        var subject = template.SubjectTemplate is null
            ? null
            : ReplaceTokens(template.SubjectTemplate, data);
        var body = ReplaceTokens(template.BodyTemplate, data);
        return new RenderedTemplate(subject, body);
    }

    private static bool HasValue(IReadOnlyDictionary<string, string?> data, string key) =>
        data.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v);

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string?> data)
    {
        return TokenPattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            // Optional tokens with no value render as empty string (deterministic),
            // not as raw {{token}} — the strict guard above ensures only optional
            // tokens can reach this branch with no value.
            return data.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
        });
    }
}

public sealed class UnknownTemplateException : Exception
{
    public string TemplateKey { get; }

    public UnknownTemplateException(string templateKey)
        : base($"Notification template '{templateKey}' is not registered.")
    {
        TemplateKey = templateKey;
    }
}

public sealed class MissingRequiredTokenException : Exception
{
    public string TemplateKey { get; }
    public IReadOnlyList<string> MissingTokens { get; }

    public MissingRequiredTokenException(string templateKey, IReadOnlyList<string> missingTokens)
        : base($"Notification template '{templateKey}' is missing required token(s): {string.Join(", ", missingTokens)}.")
    {
        TemplateKey  = templateKey;
        MissingTokens = missingTokens;
    }
}
