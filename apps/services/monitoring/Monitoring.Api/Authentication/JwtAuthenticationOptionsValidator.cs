namespace Monitoring.Api.Authentication;

/// <summary>
/// RETIRED — MON-INT-01-003.
/// <para>
/// Previously validated the RS256 <see cref="JwtAuthenticationOptions"/> at
/// startup. No longer referenced; options validation is now handled inline in
/// <see cref="AuthenticationServiceCollectionExtensions"/> (throws on missing
/// <c>Jwt:SigningKey</c> at service startup).
/// </para>
/// </summary>
[Obsolete("Retired in MON-INT-01-003. Options validation is now inline in AddMonitoringAuthentication.")]
internal sealed class JwtAuthenticationOptionsValidator { }
