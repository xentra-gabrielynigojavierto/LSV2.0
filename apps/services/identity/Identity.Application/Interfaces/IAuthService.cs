using System.Security.Claims;
using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>
    /// Builds an AuthMeResponse from a validated ClaimsPrincipal.
    /// Called by GET /api/auth/me after JWT validation — no DB lookup required
    /// for the basic session fields since all data is encoded in the token.
    /// </summary>
    Task<AuthMeResponse> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}
