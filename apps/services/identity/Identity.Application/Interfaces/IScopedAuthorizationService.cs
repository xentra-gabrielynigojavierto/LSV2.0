using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

/// <summary>
/// Checks scope-aware role authorization against ScopedRoleAssignments.
///
/// Phase I: moves scoped authorization from "platform capability" to
/// "real exercised runtime pattern".  All checks honour the precedence rule:
///   GLOBAL scope always satisfies any narrower scope check.
/// </summary>
public interface IScopedAuthorizationService
{
    /// <summary>
    /// Returns true if the user holds the named role with ORGANIZATION scope
    /// matching <paramref name="organizationId"/>, or holds it with GLOBAL scope.
    /// </summary>
    Task<bool> HasOrganizationRoleAsync(
        Guid   userId,
        string roleName,
        Guid   organizationId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if the user holds the named role with PRODUCT scope
    /// matching <paramref name="productId"/>, or holds it with GLOBAL scope.
    /// </summary>
    Task<bool> HasProductRoleAsync(
        Guid   userId,
        string roleName,
        Guid   productId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all active scoped role assignments for the user, ordered by
    /// ScopeType, for diagnostic / admin display.
    /// </summary>
    Task<ScopedRoleSummaryResponse> GetScopedRoleSummaryAsync(
        Guid   userId,
        CancellationToken ct = default);
}
