using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(
        User user,
        Tenant tenant,
        IEnumerable<string> roles,
        Organization? organization = null,
        IEnumerable<string>? productRoles = null,
        int? sessionTimeoutMinutes = null,
        IEnumerable<string>? productCodes = null,
        IEnumerable<string>? permissions = null);
}
