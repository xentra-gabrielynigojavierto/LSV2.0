using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication.ServiceTokens;

/// <summary>
/// LS-FLOW-MERGE-P5 — default <see cref="IServiceTokenIssuer"/>. HS256
/// tokens with a 5-minute default lifetime; safe to call per-request as
/// the cost is a single signature.
/// </summary>
public sealed class ServiceTokenIssuer : IServiceTokenIssuer
{
    private readonly ServiceTokenOptions _options;

    public ServiceTokenIssuer(IOptions<ServiceTokenOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SigningKey);

    public string IssueToken(string tenantId, string? actorUserId = null)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                $"Service-token signing key is not configured. Set {ServiceTokenAuthenticationDefaults.SecretEnvVar} or ServiceTokens:SigningKey.");

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId is required for service tokens.", nameof(tenantId));

        var now = DateTime.UtcNow;
        var lifetime = _options.LifetimeMinutes > 0 ? _options.LifetimeMinutes : ServiceTokenAuthenticationDefaults.DefaultLifetimeMinutes;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,  $"service:{_options.ServiceName}"),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString("N")),
            new(ServiceTokenAuthenticationDefaults.TenantClaim, tenantId),
            // Emit the platform-standard "tenant_id" claim too so existing
            // tenant resolvers (CurrentRequestContext, ClaimsTenantProvider,
            // RequireProductAccessFilter) accept service tokens unchanged.
            new("tenant_id", tenantId),
            new(ClaimTypes.Role, ServiceTokenAuthenticationDefaults.ServiceRole),
            new("svc", _options.ServiceName)
        };
        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            claims.Add(new Claim(ServiceTokenAuthenticationDefaults.ActorClaim, $"user:{actorUserId}"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
