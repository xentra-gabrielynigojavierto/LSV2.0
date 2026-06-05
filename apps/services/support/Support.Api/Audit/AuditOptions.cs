namespace Support.Api.Audit;

public enum AuditDispatchMode
{
    NoOp = 0,
    Http = 1,
}

/// <summary>
/// Bound from configuration section <c>Support:Audit</c>.
/// </summary>
public sealed class AuditOptions
{
    public const string SectionName = "Support:Audit";

    /// <summary>Master kill-switch; when false, audit dispatch is suppressed.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Audit Service base URL. Required when Mode = Http.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP client timeout in seconds. Defaults to 5s.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Dispatch transport. Defaults to NoOp for safe local/test runs.</summary>
    public AuditDispatchMode Mode { get; set; } = AuditDispatchMode.NoOp;
}
