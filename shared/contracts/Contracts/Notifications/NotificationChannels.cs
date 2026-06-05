namespace Contracts.Notifications;

/// <summary>
/// Channel-neutral channel identifiers for the platform notification contract (E12.1).
///
/// <para>
/// Producers (Flow, Tasks, SLA, Admin) target one or more channels by string
/// constant rather than an enum so adding new channels (push, webhook, sms)
/// is a non-breaking, additive change. The notifications service is the only
/// place that decides which channels are actually wired to providers.
/// </para>
///
/// <para>
/// Currently supported in production:
/// <list type="bullet">
///   <item><description><see cref="Email"/> — outbound transactional email via the notifications service.</description></item>
///   <item><description><see cref="InApp"/> — in-app inbox / banner channel (full UX deferred to E12.x).</description></item>
/// </list>
/// </para>
///
/// <para>
/// Reserved placeholders (no provider wiring yet, but accepted by the contract):
/// <list type="bullet">
///   <item><description><see cref="Sms"/></description></item>
///   <item><description><see cref="Webhook"/></description></item>
///   <item><description><see cref="Push"/></description></item>
/// </list>
/// Producers may declare these in <c>NotificationEnvelope.ChannelHints</c>;
/// the notifications service will currently treat them as a no-op rather than
/// rejecting the envelope, so future wiring is purely additive.
/// </para>
/// </summary>
public static class NotificationChannels
{
    public const string Email   = "email";
    public const string InApp   = "in_app";
    public const string Sms     = "sms";
    public const string Webhook = "webhook";
    public const string Push    = "push";

    /// <summary>Channels that have a provider adapter wired today.</summary>
    public static readonly IReadOnlyList<string> Supported = new[] { Email, InApp };

    /// <summary>Channels reserved by the contract but not yet wired to a provider.</summary>
    public static readonly IReadOnlyList<string> Reserved = new[] { Sms, Webhook, Push };

    /// <summary>True when the channel is a known contract value (supported or reserved).</summary>
    public static bool IsKnown(string channel) =>
        Supported.Contains(channel) || Reserved.Contains(channel);
}
