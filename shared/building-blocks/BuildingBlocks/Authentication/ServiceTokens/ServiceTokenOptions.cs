namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 — bind from the <c>ServiceTokens</c> configuration
/// section in product apps. The signing key is required and is normally
/// supplied via the <c>FLOW_SERVICE_TOKEN_SECRET</c> environment
/// variable rather than appsettings to keep secrets out of source.
/// </summary>
public sealed class ServiceTokenOptions
{
    public const string SectionName = "ServiceTokens";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = ServiceTokenAuthenticationDefaults.DefaultIssuer;
    public string Audience { get; set; } = ServiceTokenAuthenticationDefaults.DefaultAudience;
    public int LifetimeMinutes { get; set; } = ServiceTokenAuthenticationDefaults.DefaultLifetimeMinutes;
    public string ServiceName { get; set; } = "unknown-service";
}
