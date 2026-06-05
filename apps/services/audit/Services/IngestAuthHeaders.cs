namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical HTTP header names for the ingest authentication protocol.
///
/// These constants are shared between <see cref="Middleware.IngestAuthMiddleware"/>,
/// <see cref="IIngestAuthenticator"/> implementations, and documentation.
/// Centralising them here prevents header name drift if the contract evolves.
///
/// Header contract (all headers are case-insensitive per RFC 7230):
///
///   x-service-token  — Required in ServiceToken mode. The shared secret credential.
///                      Generated with: openssl rand -base64 32
///
///   x-source-system  — Optional. Logical source system name (e.g. "identity-service").
///                      Used for logging/enrichment only; does not affect authentication.
///                      When AllowedSources is configured, this header is validated.
///
///   x-source-service — Optional. Sub-component within the source system.
///                      Used for logging/enrichment only; does not affect authentication.
/// </summary>
public static class IngestAuthHeaders
{
    /// <summary>Required shared secret in ServiceToken mode.</summary>
    public const string ServiceToken  = "x-service-token";

    /// <summary>Optional: logical source system identifier.</summary>
    public const string SourceSystem  = "x-source-system";

    /// <summary>Optional: sub-component or microservice within the source system.</summary>
    public const string SourceService = "x-source-service";
}
