namespace Support.Api.Notifications;

public enum NotificationDispatchMode
{
    NoOp = 0,
    Http = 1,
}

/// <summary>
/// Bound from configuration section <c>Support:Notifications</c>.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Support:Notifications";

    /// <summary>Master kill-switch; when false, all dispatch is suppressed.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Notifications Service base URL. Required when Mode = Http.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP client timeout in seconds. Defaults to 5s.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Dispatch transport. Defaults to NoOp for safe local/test runs.</summary>
    public NotificationDispatchMode Mode { get; set; } = NotificationDispatchMode.NoOp;

    /// <summary>
    /// Base URL of the tenant portal, used to build deeplinks in notification emails.
    /// Example: https://portal.legalsynq.com
    /// When unset, deeplink_url is omitted from template data.
    /// </summary>
    public string? PortalBaseUrl { get; set; }

    /// <summary>
    /// Plain domain name (no scheme) used to build tenant-subdomain deeplinks.
    /// Example: legalsynqplatform.replit.app
    /// When set, deeplinks are constructed as https://{tenantId}.{PortalBaseDomain}/support/{ticketId}.
    /// Takes precedence over <see cref="PortalBaseUrl"/> for deeplink construction.
    /// </summary>
    public string? PortalBaseDomain { get; set; }

    /// <summary>
    /// Base URL of the Control Center, used to build admin deeplinks in notification emails.
    /// Example: https://cc.legalsynq.com
    /// The live value from <c>platform.controlCenterBaseUrl</c> (persisted in the Tenant DB via
    /// the Control Center Settings page) always takes precedence over this fallback.
    /// When neither is set, admin recipients receive the same tenant-portal deeplink.
    /// </summary>
    public string? ControlCenterBaseUrl { get; set; }
}
