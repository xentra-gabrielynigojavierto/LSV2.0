namespace PlatformAuditEventService.Services;

/// <summary>
/// Carries the authenticated service identity for a single request.
///
/// Set by <see cref="Middleware.IngestAuthMiddleware"/> in <c>HttpContext.Items</c>
/// after a successful authentication so downstream controllers and services can
/// access caller identity without repeating header parsing.
///
/// Access pattern in a controller:
/// <code>
///   var ctx = HttpContext.Items[ServiceAuthContext.ItemKey] as ServiceAuthContext;
/// </code>
///
/// The context is intentionally read-only after construction — middleware sets it once
/// and the rest of the pipeline consumes it.
/// </summary>
public sealed class ServiceAuthContext
{
    /// <summary>Key used to store/retrieve this context from <c>HttpContext.Items</c>.</summary>
    public const string ItemKey = "IngestAuth.ServiceAuthContext";

    /// <summary>
    /// Logical name of the authenticated service, from the matching
    /// <see cref="Configuration.ServiceTokenEntry.ServiceName"/> or
    /// the "anonymous" literal when running in None/pass-through mode.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Value of the <c>x-source-system</c> request header, if provided.
    /// Optional — callers may omit this header; it does not affect authentication.
    /// When present, it is logged and can be used for per-source routing or auditing.
    /// </summary>
    public string? SourceSystem { get; init; }

    /// <summary>
    /// Value of the <c>x-source-service</c> request header, if provided.
    /// Optional — identifies the specific sub-component or microservice within SourceSystem.
    /// </summary>
    public string? SourceService { get; init; }

    /// <summary>
    /// Auth mode that produced this context.
    /// Matches <c>IngestAuth:Mode</c> from configuration.
    /// </summary>
    public string AuthMode { get; init; } = string.Empty;
}
