using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for asynchronous audit export jobs.
///
/// Unlike audit records, export jobs have a mutable lifecycle:
/// Pending → Processing → Completed | Failed | Cancelled | Expired.
/// The <see cref="UpdateAsync"/> method is intentionally narrow — callers
/// pass the full entity with modified mutable fields so EF Core tracks
/// the delta, keeping the contract simple without a custom patch DTO.
/// </summary>
public interface IAuditExportJobRepository
{
    /// <summary>
    /// Persist a new export job. ExportId must be unique (unique index enforced).
    /// </summary>
    Task<AuditExportJob> CreateAsync(AuditExportJob job, CancellationToken ct = default);

    /// <summary>
    /// Retrieve an export job by its public <see cref="AuditExportJob.ExportId"/>.
    /// Returns null if not found.
    /// </summary>
    Task<AuditExportJob?> GetByExportIdAsync(Guid exportId, CancellationToken ct = default);

    /// <summary>
    /// Persist mutable lifecycle changes (Status, FilePath, ErrorMessage, CompletedAtUtc).
    /// Callers must have retrieved the entity via <see cref="GetByExportIdAsync"/> first
    /// to ensure EF's change tracker has a baseline.
    /// </summary>
    Task<AuditExportJob> UpdateAsync(AuditExportJob job, CancellationToken ct = default);

    /// <summary>
    /// List export jobs submitted by a specific actor, newest first, with pagination.
    /// </summary>
    Task<PagedResult<AuditExportJob>> ListByRequesterAsync(
        string requestedBy,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// List export jobs that are currently in Pending or Processing state.
    /// Used by the export worker to pick up pending work.
    /// </summary>
    Task<IReadOnlyList<AuditExportJob>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Paginated list of export jobs filtered to a specific set of statuses, newest first.
    /// Useful for admin dashboards and worker monitoring (e.g. list all Failed jobs,
    /// list all jobs for a given scope).
    /// Pass an empty <paramref name="statuses"/> array to return all statuses.
    /// </summary>
    Task<PagedResult<AuditExportJob>> ListByStatusAsync(
        IReadOnlyList<ExportStatus> statuses,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
