using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(
        User user,
        Tenant tenant,
        IEnumerable<string> roles,
        Organization? organization = null,
        IEnumerable<string>? productRoles = null,
        int? sessionTimeoutMinutes = null,
        IEnumerable<string>? productCodes = null,
        IEnumerable<string>? permissions = null)
    {
        var section = _configuration.GetSection("Jwt");

        var issuer = section["Issuer"] ?? "legalsynq-identity";
        var audience = section["Audience"] ?? "legalsynq-platform";
        var signingKey = section["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var expiryMinutes = int.TryParse(section["ExpiryMinutes"], out var m) ? m : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenant.Id.ToString()),
            new("tenant_code", tenant.Code),
            // UIX-003-03: session invalidation — embed version at login time.
            // auth/me validates this against the DB value; mismatches reject the request.
            new("session_version", user.SessionVersion.ToString()),
            // LS-COR-AUT-003: access freshness — embed version at login time.
            new("access_version", user.AccessVersion.ToString()),
        };

        // Use the short JWT claim name "role" (not ClaimTypes.Role which serializes
        // to the long Microsoft URI). The identity service sets MapInboundClaims=false,
        // so auth/me reads principal.FindAll("role") — the short name must match.
        foreach (var role in roles)
            claims.Add(new Claim("role", role));

        if (organization is not null)
        {
            claims.Add(new Claim("org_id", organization.Id.ToString()));

            // Phase H: derive org_type code from the canonical OrganizationTypeId FK first;
            // fall back to the stored OrgType string for compatibility with old rows.
            // TODO [Phase H — remove OrgType string]: once column is dropped, always use OrgTypeMapper.
            var orgTypeCode = Identity.Domain.OrgTypeMapper.TryResolveCode(organization.OrganizationTypeId)
                ?? organization.OrgType;
            claims.Add(new Claim("org_type", orgTypeCode));

            // Also emit the canonical OrganizationTypeId for consumers that understand the catalog.
            if (organization.OrganizationTypeId.HasValue)
                claims.Add(new Claim("org_type_id", organization.OrganizationTypeId.Value.ToString()));

            claims.Add(new Claim("provider_mode", Identity.Domain.ProviderModes.Normalize(organization.ProviderMode)));
        }

        foreach (var pc in productCodes ?? [])
            claims.Add(new Claim("product_codes", pc));

        foreach (var pr in productRoles ?? [])
            claims.Add(new Claim("product_roles", pr));

        foreach (var perm in permissions ?? [])
            claims.Add(new Claim("permissions", perm));

        // Embed per-tenant idle session timeout so GetCurrentUserAsync needs no DB lookup.
        var effectiveTimeout = sessionTimeoutMinutes ?? 30;
        claims.Add(new Claim("session_timeout_minutes", effectiveTimeout.ToString()));

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
