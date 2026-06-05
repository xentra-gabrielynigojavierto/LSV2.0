namespace Reports.Contracts.Context;

/// <summary>
/// Exposes the authenticated tenant and user identity for the current HTTP request.
/// Resolved from JWT claims by the infrastructure layer — not from client-supplied request values.
/// </summary>
public interface ICurrentTenantContext
{
    /// <summary>Tenant ID from the authenticated JWT claim. Null if unauthenticated or claim absent.</summary>
    string? TenantId { get; }

    /// <summary>User ID from the authenticated JWT claim. Null if unauthenticated or claim absent.</summary>
    string? UserId { get; }
}
