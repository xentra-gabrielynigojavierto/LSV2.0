using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for integrity checkpoints.
///
/// Checkpoints are append-only; re-runs always create a new record.
/// There is no update or delete operation — tampering with checkpoint
/// records would itself be detectable via the audit log.
/// </summary>
public interface IIntegrityCheckpointRepository
{
    /// <summary>
    /// Persist a new integrity checkpoint record.
    /// </summary>
    Task<IntegrityCheckpoint> AppendAsync(
        IntegrityCheckpoint checkpoint,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve a checkpoint by its surrogate primary key.
    /// Returns null if not found.
    /// </summary>
    Task<IntegrityCheckpoint?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Retrieve the most recently created checkpoint of the given type.
    /// Returns null if no checkpoint of that type exists yet.
    /// Used to find the last verified baseline before computing a new one.
    /// </summary>
    Task<IntegrityCheckpoint?> GetLatestAsync(
        string checkpointType,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve all checkpoints within the given time window (inclusive on both ends,
    /// based on <see cref="IntegrityCheckpoint.FromRecordedAtUtc"/>).
    /// Typically used by verification jobs to find relevant checkpoints to re-verify.
    /// </summary>
    Task<IReadOnlyList<IntegrityCheckpoint>> GetByWindowAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>
    /// Paginated list of checkpoints filtered to a specific cadence label, newest first.
    /// </summary>
    Task<PagedResult<IntegrityCheckpoint>> ListByTypeAsync(
        string checkpointType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Paginated, multi-filter list of checkpoints — newest first.
    ///
    /// All filter parameters are optional:
    /// <list type="bullet">
    ///   <item><paramref name="checkpointType"/>: exact match on <see cref="IntegrityCheckpoint.CheckpointType"/>; pass null to include all types.</item>
    ///   <item><paramref name="from"/>: include only checkpoints with <c>CreatedAtUtc &gt;= from</c>.</item>
    ///   <item><paramref name="to"/>: include only checkpoints with <c>CreatedAtUtc &lt;= to</c>.</item>
    /// </list>
    /// </summary>
    Task<PagedResult<IntegrityCheckpoint>> ListAsync(
        string?        checkpointType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int            page,
        int            pageSize,
        CancellationToken ct = default);
}
