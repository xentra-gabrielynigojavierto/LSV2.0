namespace LegalSynq.AuditClient;

/// <summary>
/// Bound from "AuditClient" in the producer service's appsettings.json.
/// </summary>
public sealed class AuditClientOptions
{
    public const string SectionName = "AuditClient";

    /// <summary>Base URL of the Platform Audit Event Service. Example: http://localhost:5007</summary>
    public string BaseUrl { get; set; } = "http://localhost:5007";

    /// <summary>x-service-token value. Leave empty in development (Mode=None ignores the header).</summary>
    public string ServiceToken { get; set; } = string.Empty;

    /// <summary>Logical name of the producing system. Used in SourceSystem when building requests.</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Sub-component within SourceSystem. Optional.</summary>
    public string? SourceService { get; set; }

    /// <summary>HTTP timeout in seconds. Default 5 s — audit must never block business operations.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
