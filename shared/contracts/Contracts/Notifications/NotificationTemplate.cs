namespace Contracts.Notifications;

/// <summary>
/// Reusable, channel-neutral template definition (E12.1). One definition
/// covers all channels it is applicable to (subject + body templates may
/// differ per channel; if not provided, the body template is used as-is).
///
/// <para>
/// Templates are addressed by <see cref="Key"/>. Producers reference keys;
/// the notifications service is responsible for resolving the active
/// version (tenant overrides, branding, locale) at render time.
/// </para>
/// </summary>
public sealed record NotificationTemplate
{
    /// <summary>Stable, dotted template key (e.g. <c>workflow.completed</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name for operators / template editors.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Channels this template is applicable to. If a producer requests a
    /// channel not in this list, the notifications service skips that
    /// channel rather than rejecting the envelope.
    /// </summary>
    public required IReadOnlyList<string> Channels { get; init; }

    /// <summary>Default subject template (Mustache-style <c>{{token}}</c>). Optional for body-only channels.</summary>
    public string? SubjectTemplate { get; init; }

    /// <summary>Default body template (Mustache-style <c>{{token}}</c>).</summary>
    public required string BodyTemplate { get; init; }

    /// <summary>Token contract — declares which variables the body / subject expect, and which are required.</summary>
    public IReadOnlyList<NotificationTokenDefinition> Tokens { get; init; } = Array.Empty<NotificationTokenDefinition>();

    /// <summary>One of <see cref="NotificationSeverity"/>.</summary>
    public string Severity { get; init; } = NotificationSeverity.Info;

    /// <summary>One of <see cref="NotificationCategory"/>.</summary>
    public string Category { get; init; } = NotificationCategory.System;

    /// <summary>Templates can be disabled by ops without removing the registry entry.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>True iff this template advertises support for <paramref name="channel"/>.</summary>
    public bool SupportsChannel(string channel) => Channels.Contains(channel);
}

/// <summary>
/// Declares one variable a template body / subject expects.
/// </summary>
public sealed record NotificationTokenDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; } = true;
}
