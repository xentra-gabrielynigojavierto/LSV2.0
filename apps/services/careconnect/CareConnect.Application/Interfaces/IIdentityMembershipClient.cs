// BLK-CC-01: Typed client abstraction for Identity membership calls from CareConnect.
// Identity service = membership / access (assign user to tenant, assign roles).
// Calls the BLK-ID-02 APIs: POST /api/internal/users/assign-tenant
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Cross-service client for Identity membership APIs.
/// Used during provider onboarding to assign the existing provider user
/// to the newly provisioned tenant.
/// </summary>
public interface IIdentityMembershipClient
{
    /// <summary>
    /// Assigns an existing Identity user to a tenant and grants the specified roles.
    /// Calls POST /api/internal/users/assign-tenant on the Identity service.
    ///
    /// Idempotent — safe to call if the user is already in the target tenant.
    ///
    /// Returns the assignment result on success.
    /// Returns null on any infrastructure failure (network, timeout, 5xx, 401).
    /// </summary>
    Task<IdentityTenantAssignmentResult?> AssignTenantAsync(
        Guid              userId,
        Guid              tenantId,
        IList<string>     roles,
        CancellationToken ct = default);
}

// ── Result types ───────────────────────────────────────────────────────────────

public sealed class IdentityTenantAssignmentResult
{
    public Guid UserId          { get; init; }
    public Guid TenantId        { get; init; }
    public bool AlreadyInTenant { get; init; }
}
