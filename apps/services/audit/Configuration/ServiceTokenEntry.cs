namespace PlatformAuditEventService.Configuration;

/// <summary>
/// A single named service credential in the ServiceToken auth registry.
///
/// Each entry represents one authorized caller (microservice, worker, or integration).
/// Storing tokens as named entries — rather than a single shared key — allows:
///   - Per-service token rotation without affecting other callers.
///   - Per-service revocation (set Enabled = false) without a code deploy.
///   - Audit logs that identify which service submitted an event.
///
/// Security guidance:
///   - Generate tokens with at least 256 bits of entropy: openssl rand -base64 32
///   - Store tokens as environment variable overrides, not in committed appsettings files.
///   - Rotate tokens on a scheduled cadence; use overlapping validity windows to avoid downtime.
///   - Never reuse a token across environments (dev / staging / prod).
///
/// Config path: IngestAuth:ServiceTokens[n]
/// Environment variable override (n=0): IngestAuth__ServiceTokens__0__Token
/// </summary>
public sealed record ServiceTokenEntry
{
    /// <summary>
    /// The shared secret token value. Min 32 chars recommended.
    /// MUST be injected via environment variable — never commit real tokens.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Logical name of the service that holds this token.
    /// Propagated into audit logs and the <see cref="PlatformAuditEventService.Services.ServiceAuthContext"/> for traceability.
    /// Examples: "identity-service", "fund-service", "care-connect-api".
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of this credential entry.
    /// Not used for auth — purely for operator clarity in configuration files.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When false, this entry is ignored during authentication.
    /// Use to revoke a specific service's access without removing the config entry.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
