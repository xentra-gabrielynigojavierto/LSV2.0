using Documents.Domain.Entities;

namespace Documents.Domain.Interfaces;

public interface IAuditRepository
{
    /// <summary>Insert an audit event. Must not throw — failures are logged but non-fatal.</summary>
    Task InsertAsync(DocumentAudit audit, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentAudit>> ListForDocumentAsync(Guid documentId, Guid tenantId, int limit = 200, CancellationToken ct = default);
}
