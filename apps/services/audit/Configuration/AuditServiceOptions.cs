namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Top-level service identity and behavior options.
/// Bound from "AuditService" section in appsettings.
/// Environment variable override prefix: AuditService__
///
/// This class covers service-level settings only.
/// See DatabaseOptions, IntegrityOptions, IngestAuthOptions, QueryAuthOptions,
/// RetentionOptions, and ExportOptions for domain-specific configuration areas.
/// </summary>
public sealed class AuditServiceOptions
{
    public const string SectionName = "AuditService";

    /// <summary>Service display name included in health responses and logs.</summary>
    public string ServiceName { get; set; } = "Platform Audit/Event Service";

    /// <summary>Service version string.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Environment tag surfaced in health checks and Swagger info.
    /// Defaults to ASPNETCORE_ENVIRONMENT when not explicitly set.
    /// </summary>
    public string? EnvironmentTag { get; set; }

    /// <summary>
    /// When true, Swagger UI is accessible regardless of environment.
    /// Defaults to false — UI is only shown in Development unless overridden.
    /// NEVER set to true in public-facing production deployments.
    /// Environment variable: AuditService__ExposeSwagger
    /// </summary>
    public bool ExposeSwagger { get; set; } = false;

    /// <summary>
    /// Allowed CORS origins. Empty = deny all cross-origin requests.
    /// Use ["*"] in development only — restrict to known origins in production.
    /// Environment variable: AuditService__AllowedCorsOrigins__0, __1, ...
    /// </summary>
    public List<string> AllowedCorsOrigins { get; set; } = [];
}
