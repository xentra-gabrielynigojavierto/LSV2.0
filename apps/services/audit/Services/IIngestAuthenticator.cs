using Microsoft.AspNetCore.Http;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Contract for a pluggable ingest authentication strategy.
///
/// The active implementation is selected at startup based on <c>IngestAuth:Mode</c>
/// and registered as <c>IIngestAuthenticator</c> in the DI container.
/// <see cref="Middleware.IngestAuthMiddleware"/> calls this interface — it has no
/// knowledge of the underlying auth mechanism.
///
/// Extensibility:
///   Adding a new auth mode (JWT, mTLS, service mesh) requires:
///     1. Implement <see cref="IIngestAuthenticator"/> in a new class.
///     2. Register it in Program.cs and add the mode string to the factory switch.
///   No changes to middleware, controllers, or validators are needed.
///
/// Currently supported modes:
///   "None"         → <see cref="NullIngestAuthenticator"/>   (dev/test pass-through, never use in prod)
///   "ServiceToken" → <see cref="ServiceTokenAuthenticator"/> (shared secrets per named service)
///
/// Planned future modes (not yet implemented):
///   "Bearer"       → JWT validation via Microsoft.IdentityModel
///   "MtlsHeader"   → mTLS client certificate forwarded by a proxy/service mesh sidecar
///   "MeshInternal" → Trust-on-network (Istio/Linkerd service identity, no token required)
/// </summary>
public interface IIngestAuthenticator
{
    /// <summary>
    /// The auth mode string this implementation handles.
    /// Used for logging and diagnostics only — not for routing decisions.
    /// </summary>
    string Mode { get; }

    /// <summary>
    /// Attempt to authenticate the inbound request based on its headers.
    ///
    /// Implementations MUST use constant-time comparison when validating secrets
    /// to prevent timing-based side-channel attacks.
    ///
    /// Implementations MUST NOT throw on invalid credentials — return a failed
    /// <see cref="AuthResult"/> instead so the middleware can produce a consistent
    /// 401 response.
    /// </summary>
    /// <param name="headers">The full inbound request header dictionary.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="AuthResult"/> indicating success or failure.
    /// On success, <see cref="AuthResult.ServiceName"/> identifies the authenticated caller.
    /// </returns>
    Task<AuthResult> AuthenticateAsync(IHeaderDictionary headers, CancellationToken ct = default);
}

/// <summary>
/// Outcome of an <see cref="IIngestAuthenticator.AuthenticateAsync"/> call.
/// </summary>
/// <param name="Succeeded">True when the request is authenticated and may proceed.</param>
/// <param name="ServiceName">
/// Logical name of the authenticated caller. Populated on success; null on failure.
/// Sourced from the matching <see cref="Configuration.ServiceTokenEntry.ServiceName"/>.
/// </param>
/// <param name="SourceSystem">
/// Value of the <c>x-source-system</c> request header, if present.
/// Not validated by the authenticator — provided for downstream enrichment.
/// </param>
/// <param name="SourceService">
/// Value of the <c>x-source-service</c> request header, if present.
/// Not validated by the authenticator — provided for downstream enrichment.
/// </param>
/// <param name="Reason">
/// Machine-readable reason for authentication failure. Null on success.
/// Values: "MissingToken" | "InvalidToken" | "DisabledEntry" | "TokenNotConfigured"
/// </param>
public sealed record AuthResult(
    bool    Succeeded,
    string? ServiceName   = null,
    string? SourceSystem  = null,
    string? SourceService = null,
    string? Reason        = null)
{
    /// <summary>Pre-built success result with no identity (used by NullIngestAuthenticator).</summary>
    public static readonly AuthResult AnonymousSuccess =
        new(Succeeded: true, ServiceName: "anonymous");

    /// <summary>Pre-built failure when the token header is absent.</summary>
    public static readonly AuthResult Missing =
        new(Succeeded: false, Reason: "MissingToken");

    /// <summary>Pre-built failure when the provided token doesn't match any registry entry.</summary>
    public static readonly AuthResult Invalid =
        new(Succeeded: false, Reason: "InvalidToken");

    /// <summary>Pre-built failure when no service tokens are configured in the registry.</summary>
    public static readonly AuthResult NotConfigured =
        new(Succeeded: false, Reason: "TokenNotConfigured");
}
