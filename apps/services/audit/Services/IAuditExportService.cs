using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs.Export;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Orchestrates the full audit export lifecycle:
///
///   1. Validate and authorize the <see cref="DTOs.Export.ExportRequest"/>.
///   2. Persist an <see cref="Entities.AuditExportJob"/> in the Pending state.
///   3. Process the export (v1: synchronous in-request; future: background worker).
///   4. Write the output file via <see cref="Export.IExportStorageProvider"/>.
///   5. Transition the job to Completed or Failed and persist the result.
///   6. Return an <see cref="ExportStatusResponse"/> reflecting the terminal state.
///
/// Status polling is served by <see cref="GetStatusAsync"/>, which reads the
/// persisted job record without re-running the export logic.
/// </summary>
public interface IAuditExportService
{
    /// <summary>
    /// Submit a new export job.
    ///
    /// Authorization is performed using the provided <paramref name="caller"/> context
    /// (resolved by <c>QueryAuthMiddleware</c>). The same scope constraints applied
    /// to query endpoints are enforced here — cross-tenant requests and scope
    /// escalation are denied.
    ///
    /// v1 processes the export synchronously within the HTTP request. The job
    /// transitions through Pending → Processing → Completed (or Failed) before
    /// this method returns. The response reflects the terminal state.
    ///
    /// Throws <see cref="UnauthorizedAccessException"/> when the caller lacks
    /// permission for the requested scope. Callers should catch and return 403.
    /// </summary>
    Task<ExportStatusResponse> SubmitAsync(
        ExportRequest        request,
        IQueryCallerContext  caller,
        CancellationToken    ct = default);

    /// <summary>
    /// Return the current status of an existing export job.
    ///
    /// Access is restricted to the job's original requester and to PlatformAdmin callers.
    /// Returns null when no job with the given <paramref name="exportId"/> exists OR
    /// when the caller is not permitted to view that job.
    /// </summary>
    /// <param name="exportId">The public export job identifier.</param>
    /// <param name="caller">
    /// The caller's resolved authorization context. Used to verify that the caller
    /// owns the job or holds PlatformAdmin scope before returning the result.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ExportStatusResponse?> GetStatusAsync(
        Guid                exportId,
        IQueryCallerContext caller,
        CancellationToken   ct = default);

    /// <summary>
    /// Process an export job that is already in the database (Pending or Processing state).
    ///
    /// Called by <see cref="Jobs.ExportProcessingJob"/> when processing export jobs asynchronously
    /// in a background worker instead of synchronously within an HTTP request.
    ///
    /// Fetches the job by ExportId, deserialises the stored FilterJson, and drives the
    /// Pending → Processing → Completed/Failed state machine.
    ///
    /// Returns without throwing — any job-level failure transitions the job to Failed.
    /// Returns without action when the job is not found or is already in a terminal state.
    /// </summary>
    Task ProcessJobAsync(Guid exportId, CancellationToken ct = default);
}
