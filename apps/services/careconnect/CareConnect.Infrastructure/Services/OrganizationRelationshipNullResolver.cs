using CareConnect.Application.Interfaces;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Null-object implementation of IOrganizationRelationshipResolver.
/// Always returns null — safe default when the Identity HTTP resolver is not configured.
///
/// Phase C — TODO: replace with HttpOrganizationRelationshipResolver that calls
/// GET /identity/api/admin/organization-relationships?sourceOrgId=X&activeOnly=true
/// to resolve the OrganizationRelationshipId from Identity service.
/// </summary>
public sealed class OrganizationRelationshipNullResolver : IOrganizationRelationshipResolver
{
    public Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);
}
