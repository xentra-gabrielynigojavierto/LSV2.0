using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for ingest source registrations.
///
/// The (SourceSystem, SourceService) pair is the natural unique key.
/// Upsert semantics: if a record with the same key already exists,
/// its mutable fields (IsActive, Notes) are updated; otherwise a new
/// record is inserted.
/// </summary>
public interface IIngestSourceRegistrationRepository
{
    /// <summary>
    /// Insert or update a source registration by its (SourceSystem, SourceService) key.
    /// Returns the persisted entity after the operation.
    /// </summary>
    Task<IngestSourceRegistration> UpsertAsync(
        IngestSourceRegistration registration,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve a registration by its natural key.
    /// Pass null for <paramref name="sourceService"/> to match system-level registrations.
    /// Returns null if no matching record exists.
    /// </summary>
    Task<IngestSourceRegistration?> GetBySourceAsync(
        string sourceSystem,
        string? sourceService,
        CancellationToken ct = default);

    /// <summary>
    /// Return all active registrations. Used by the ingest pipeline to build
    /// a fast in-memory lookup for source validation.
    /// </summary>
    Task<IReadOnlyList<IngestSourceRegistration>> ListActiveAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Paginated list of all registrations (active and inactive), sorted by
    /// SourceSystem then SourceService ascending.
    /// </summary>
    Task<PagedResult<IngestSourceRegistration>> ListAllAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Toggle the IsActive flag for an existing registration.
    /// Returns the updated entity, or null if the registration is not found.
    /// </summary>
    Task<IngestSourceRegistration?> SetActiveAsync(
        string sourceSystem,
        string? sourceService,
        bool isActive,
        CancellationToken ct = default);
}
