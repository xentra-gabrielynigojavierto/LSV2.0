using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-01-002-02: Centralized provider access-readiness verification.
///
/// Evaluates whether a set of product roles satisfies the minimum access bundle
/// required for CareConnect provider referral access (receiver path).
///
/// This service is read-only and has no side effects.
/// It does NOT create users, assign roles, or link organizations.
/// </summary>
public interface IProviderAccessReadinessService
{
    /// <summary>
    /// Returns an explicit readiness result describing whether the given product roles
    /// satisfy the full CareConnect receiver-ready access bundle.
    /// </summary>
    /// <param name="productRoles">
    /// The caller's product role codes, typically sourced from <see cref="BuildingBlocks.Context.ICurrentRequestContext.ProductRoles"/>.
    /// </param>
    Task<ProviderAccessReadinessResult> GetReadinessAsync(
        IReadOnlyCollection<string> productRoles,
        CancellationToken ct = default);
}
