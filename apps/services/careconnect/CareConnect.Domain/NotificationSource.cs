namespace CareConnect.Domain;

/// <summary>
/// LSCC-005-02: Distinguishes how a notification was triggered.
/// Used to separate automatic retry lifecycle from manual operator actions.
/// </summary>
public static class NotificationSource
{
    /// <summary>The first send attempt triggered by the system on referral creation or acceptance.</summary>
    public const string Initial = "Initial";

    /// <summary>An automatic retry triggered by the background retry worker after a prior failure.</summary>
    public const string AutoRetry = "AutoRetry";

    /// <summary>A manual resend explicitly triggered by an operator through the UI or API.</summary>
    public const string ManualResend = "ManualResend";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Initial, AutoRetry, ManualResend
    };
}
