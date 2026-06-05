using Microsoft.AspNetCore.Http;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Pass-through authenticator for <c>IngestAuth:Mode = "None"</c>.
///
/// Every request is accepted unconditionally. ServiceName is set to "anonymous".
///
/// SECURITY: This implementation MUST NEVER be used in production.
/// It is intended exclusively for:
///   - Local development (no secrets infrastructure required).
///   - Automated integration tests running in an isolated CI environment.
///   - Internal developer tooling behind a firewall with no internet exposure.
///
/// A startup WARNING is logged whenever this authenticator is active so the
/// deployment is clearly flagged in logs and alerting systems.
/// </summary>
public sealed class NullIngestAuthenticator : IIngestAuthenticator
{
    /// <inheritdoc/>
    public string Mode => "None";

    /// <inheritdoc/>
    public Task<AuthResult> AuthenticateAsync(IHeaderDictionary headers, CancellationToken ct = default)
    {
        var sourceSystem  = headers.TryGetValue(IngestAuthHeaders.SourceSystem,  out var ss)  ? ss.ToString()  : null;
        var sourceService = headers.TryGetValue(IngestAuthHeaders.SourceService, out var ssvc) ? ssvc.ToString() : null;

        var result = AuthResult.AnonymousSuccess with
        {
            SourceSystem  = sourceSystem,
            SourceService = sourceService,
        };

        return Task.FromResult(result);
    }
}
