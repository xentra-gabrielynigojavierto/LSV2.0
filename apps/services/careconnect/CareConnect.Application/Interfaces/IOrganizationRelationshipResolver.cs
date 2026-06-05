namespace CareConnect.Application.Interfaces;

/// <summary>
/// Resolves the Identity OrganizationRelationshipId for a pair of organizations.
///
/// Phase C: CareConnect does not have direct access to the Identity DB.
/// This abstraction allows the relationship lookup to be provided by:
///   - A null/stub implementation (safe default — returns null)
///   - A future HTTP-based implementation that calls Identity admin endpoints
///   - A future event-sourced cache that replicates relationship data locally
///
/// Callers must treat a null return as "relationship not found" and continue
/// with OrganizationRelationshipId = null — this is always a valid state.
/// </summary>
public interface IOrganizationRelationshipResolver
{
    /// <summary>
    /// Attempt to find an active OrganizationRelationship between the two organizations.
    /// Returns the relationship's Id if found and active, otherwise null.
    /// </summary>
    Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default);
}
