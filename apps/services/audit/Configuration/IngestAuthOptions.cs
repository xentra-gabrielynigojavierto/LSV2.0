namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Authentication options for the internal audit event ingestion endpoints
/// (<c>/internal/audit/*</c>).
///
/// Bound from the <c>"IngestAuth"</c> section in appsettings.json.
/// Environment variable override prefix: <c>IngestAuth__</c>
///
/// Mode summary:
///   "None"         — no auth required. Dev/test only. NEVER use in production.
///   "ServiceToken" — validate x-service-token header against the ServiceTokens registry.
///   "Bearer"       — JWT bearer token (planned; not yet implemented).
///   "MtlsHeader"   — mTLS client certificate forwarded by a proxy (planned).
///   "MeshInternal" — service mesh identity (Istio/Linkerd, trust-on-network, planned).
///
/// Choosing a mode:
///   Development  → Mode = "None"
///   Staging/prod → Mode = "ServiceToken" with secrets in environment variables
///   Future       → Mode = "Bearer" or "MeshInternal" once identity infrastructure matures
///
/// See also: <c>Docs/ingest-auth.md</c> for the full auth flow and extension guide.
/// </summary>
public sealed class IngestAuthOptions
{
    public const string SectionName = "IngestAuth";

    // ── Mode ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Active auth mode. Case-insensitive.
    /// Allowed values: "None" | "ServiceToken" | "Bearer" (planned) | "MtlsHeader" (planned)
    /// Environment variable: IngestAuth__Mode
    /// Default: "None" (safe for development; never deploy without changing this).
    /// </summary>
    public string Mode { get; set; } = "None";

    // ── ServiceToken mode ─────────────────────────────────────────────────────

    /// <summary>
    /// Registry of named per-service credentials used in ServiceToken mode.
    ///
    /// Each entry authorizes one source system (microservice, worker, or integration).
    /// Token values MUST be injected via environment variables — never committed.
    ///
    /// Example environment variable (first entry, index 0):
    ///   IngestAuth__ServiceTokens__0__Token       = "..."
    ///   IngestAuth__ServiceTokens__0__ServiceName = "identity-service"
    ///   IngestAuth__ServiceTokens__0__Enabled     = "true"
    ///
    /// Generate token: openssl rand -base64 32
    /// </summary>
    public List<ServiceTokenEntry> ServiceTokens { get; set; } = [];

    /// <summary>
    /// When true, requests in ServiceToken mode MUST include the x-source-system header.
    /// When false (default), the header is optional and used only for logging enrichment.
    /// Does not apply in None mode.
    /// Environment variable: IngestAuth__RequireSourceSystemHeader
    /// </summary>
    public bool RequireSourceSystemHeader { get; set; } = false;

    // ── Source allowlist (applies to all modes except None) ───────────────────

    /// <summary>
    /// Allowed values for the x-source-system header.
    /// When non-empty, requests whose x-source-system value is not in this list are
    /// rejected with 403 Forbidden — even if the token is valid.
    /// Empty (default) = allow any source system.
    ///
    /// Case-insensitive comparison.
    /// Environment variable (first entry): IngestAuth__AllowedSources__0 = "identity-service"
    /// </summary>
    public List<string> AllowedSources { get; set; } = [];

    // ── Legacy / future fields (reserved) ─────────────────────────────────────

    /// <summary>
    /// [Legacy] Single API key for ingest auth when Mode = "ApiKey".
    /// Superseded by ServiceTokens registry in ServiceToken mode.
    /// MUST be injected via environment variable — never hardcode.
    /// Environment variable: IngestAuth__ApiKey
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// [Legacy] Header name to read the API key from when Mode = "ApiKey".
    /// Default: X-Api-Key. Superseded by x-service-token in ServiceToken mode.
    /// </summary>
    public string ApiKeyHeader { get; set; } = "X-Api-Key";

    /// <summary>
    /// [Reserved for Bearer mode] Required JWT claim names.
    /// All listed claims must be present in the token.
    /// </summary>
    public List<string> RequiredClaims { get; set; } = [];

    /// <summary>
    /// [Reserved for Bearer mode] Required JWT role claim value.
    /// Example: "platform-audit-ingest"
    /// </summary>
    public string? RequiredRole { get; set; }
}
