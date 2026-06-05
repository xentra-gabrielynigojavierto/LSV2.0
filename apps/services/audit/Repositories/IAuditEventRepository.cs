using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for audit event records.
/// All writes are append-only; no update or delete operations are exposed.
/// </summary>
public interface IAuditEventRepository
{
    /// <summary>Persist a new immutable audit event record.</summary>
    Task<AuditEvent> AppendAsync(AuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>Retrieve a single audit event by its unique identifier.</summary>
    Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Query audit events with optional filters and pagination.</summary>
    Task<PagedResult<AuditEvent>> QueryAsync(AuditEventQueryRequest query, CancellationToken ct = default);

    /// <summary>Count total stored events (used for health/diagnostic reporting).</summary>
    Task<long> CountAsync(CancellationToken ct = default);
}
