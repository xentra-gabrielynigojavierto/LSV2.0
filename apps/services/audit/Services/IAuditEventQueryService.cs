using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Read-only query surface for persisted audit event records.
///
/// Implementations are responsible for:
/// - Applying pagination, sorting, and all filter predicates.
/// - Mapping persistence entities to response DTOs.
/// - Conditionally populating integrity hash and redacting network identifiers
///   based on the active <see cref="Configuration.QueryAuthOptions"/>.
///
/// This interface is intentionally separate from the ingest pipeline.
/// The write path is owned by <see cref="IAuditEventIngestionService"/>.
/// </summary>
public interface IAuditEventQueryService
{
    /// <summary>
    /// Retrieve a single audit event record by its stable public identifier.
    ///
    /// When <paramref name="scopeFilter"/> is supplied the full set of scope constraints
    /// produced by <see cref="Authorization.IQueryAuthorizer"/> (TenantId, OrganizationId,
    /// ActorId, MaxVisibility, etc.) is applied to the lookup via the shared
    /// <c>ApplyFilters</c> predicate pipeline, giving this point-lookup the same
    /// isolation guarantees as a filtered list query.
    ///
    /// Pass the post-authorization <see cref="AuditEventQueryRequest"/> from the controller
    /// for user-facing lookups. Pass null only for internal callers whose access is
    /// governed outside the HTTP request path (e.g. legal-hold verification).
    ///
    /// Returns null when no record matches the AuditId or the scope constraints.
    /// </summary>
    /// <param name="auditId">The platform-assigned AuditId (not the surrogate Id).</param>
    /// <param name="scopeFilter">
    /// Post-authorization query object whose scope fields constrain the fetch.
    /// Null skips scope enforcement (internal / admin callers only).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditEventRecordResponse?> GetByAuditIdAsync(
        Guid                    auditId,
        AuditEventQueryRequest? scopeFilter = null,
        CancellationToken       ct          = default);

    /// <summary>
    /// Execute a filtered, paginated query over persisted audit event records.
    ///
    /// All filter fields on <paramref name="request"/> are optional.
    /// Unset filters are ignored — only set fields narrow the result set.
    ///
    /// The response includes pagination metadata (<c>TotalCount</c>, <c>Page</c>,
    /// <c>PageSize</c>, <c>TotalPages</c>, <c>HasNext</c>, <c>HasPrev</c>) and
    /// time-range metadata (<c>EarliestOccurredAtUtc</c>, <c>LatestOccurredAtUtc</c>)
    /// covering the full filtered result set, not just the current page.
    /// </summary>
    /// <param name="request">Filter and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditEventQueryResponse> QueryAsync(AuditEventQueryRequest request, CancellationToken ct = default);
}
