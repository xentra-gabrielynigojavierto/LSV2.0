namespace Monitoring.Api.Authentication;

/// <summary>
/// RETIRED — MON-INT-01-003.
/// <para>
/// This class was the RS256-specific configuration model for the Monitoring
/// Service's original JWT scheme. It has been replaced by the platform-standard
/// <c>Jwt:SigningKey</c> / <c>Jwt:Issuer</c> / <c>Jwt:Audience</c>
/// configuration pattern (HS256, same as Liens, Notifications, etc.).
/// </para>
/// <para>
/// Kept as an empty stub to avoid unresolved-reference errors in any
/// tooling that cached the previous build output. Will be removed in
/// a future cleanup pass once all downstream references are confirmed gone.
/// </para>
/// </summary>
[Obsolete("Retired in MON-INT-01-003. Use Jwt:SigningKey / Jwt:Issuer / Jwt:Audience instead.")]
internal sealed class JwtAuthenticationOptions
{
    internal const string SectionName = "Authentication:Jwt";
}
