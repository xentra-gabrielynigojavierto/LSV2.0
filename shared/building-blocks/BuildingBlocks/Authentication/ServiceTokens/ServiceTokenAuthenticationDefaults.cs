namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 — well-known constants for the machine-to-machine
/// service-token scheme. The shared secret is read from the environment
/// variable <see cref="SecretEnvVar"/>; tokens are HS256-signed with the
/// audience <see cref="DefaultAudience"/> and a short lifetime.
/// </summary>
public static class ServiceTokenAuthenticationDefaults
{
    /// <summary>Authentication scheme name registered alongside user JwtBearer.</summary>
    public const string Scheme = "ServiceToken";

    /// <summary>Default audience for service tokens accepted by Flow.</summary>
    public const string DefaultAudience = "flow-service";

    /// <summary>Default issuer claim for service tokens.</summary>
    public const string DefaultIssuer = "legalsynq-service-tokens";

    /// <summary>Environment variable name for the shared HS256 signing secret.</summary>
    public const string SecretEnvVar = "FLOW_SERVICE_TOKEN_SECRET";

    /// <summary>Role claim placed on service tokens so policies can branch on it.</summary>
    public const string ServiceRole = "service";

    /// <summary>Custom claim name carrying the upstream user id when a service call is acting on behalf of a user.</summary>
    public const string ActorClaim = "actor";

    /// <summary>Tenant id claim name (matches Flow's existing tenant claim).</summary>
    public const string TenantClaim = "tid";

    /// <summary>Default token lifetime in minutes.</summary>
    public const int DefaultLifetimeMinutes = 5;
}
