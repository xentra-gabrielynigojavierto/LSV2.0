namespace PlatformAuditEventService.Configuration;

/// <summary>
/// JWT Bearer authentication options for query endpoints.
/// Bound from the "Jwt" section in appsettings.
/// Environment variable override prefix: Jwt__
///
/// These options configure the ASP.NET Core JWT Bearer middleware registered in Program.cs.
/// All values should be injected via environment variables in production — never committed to config files.
///
/// Example environment variables for production:
///   Jwt__Authority    = https://your-idp.example.com/
///   Jwt__Audience     = platform-audit-api
///   Jwt__ValidIssuers__0 = https://your-idp.example.com/
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The authority (issuer URL) of the JWT identity provider.
    /// Used by the JWT middleware to discover OIDC metadata and validate token signatures.
    /// Example: "https://auth.legalsynq.com/"
    /// Environment variable: Jwt__Authority
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// The expected audience claim in issued tokens.
    /// Tokens that do not include this audience are rejected.
    /// Example: "platform-audit-api"
    /// Environment variable: Jwt__Audience
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Additional valid issuers accepted beyond the Authority.
    /// Useful when the authority URL and the iss claim differ across environments.
    /// Environment variable: Jwt__ValidIssuers__0, Jwt__ValidIssuers__1, etc.
    /// </summary>
    public List<string> ValidIssuers { get; set; } = [];

    /// <summary>
    /// When true, the JWT middleware requires HTTPS for the Authority metadata endpoint.
    /// Set to false ONLY in development environments where HTTPS is not configured.
    /// Default: true (production-safe).
    /// Environment variable: Jwt__RequireHttpsMetadata
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// When true, the JWT middleware validates the token audience against <see cref="Audience"/>.
    /// Only set to false for debugging — always true in production.
    /// Default: true.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// When true, the JWT middleware validates the token issuer.
    /// Always true in production.
    /// Default: true.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// When true, the JWT middleware validates the token lifetime (NotBefore, Expiry).
    /// Default: true.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// When true, the service will fail startup if Mode=Bearer but Authority or Audience is missing.
    /// Strongly recommended for production to prevent silent auth misconfigurations.
    /// Default: true.
    /// Environment variable: Jwt__RequireConfigurationInBearerMode
    /// </summary>
    public bool RequireConfigurationInBearerMode { get; set; } = true;

    /// <summary>
    /// Symmetric signing key used to validate JWT signatures directly, bypassing OIDC discovery.
    /// When set, <see cref="Authority"/> is ignored and the token signature is validated using
    /// a <see cref="Microsoft.IdentityModel.Tokens.SymmetricSecurityKey"/> built from this value.
    /// Must match the key used by the issuing service (e.g. Identity.Api).
    /// In production, inject via the Jwt__SigningKey environment variable — never commit a real key.
    /// Example (dev only): "dev-only-signing-key-minimum-32-chars-long!"
    /// Environment variable: Jwt__SigningKey
    /// </summary>
    public string? SigningKey { get; set; }
}
