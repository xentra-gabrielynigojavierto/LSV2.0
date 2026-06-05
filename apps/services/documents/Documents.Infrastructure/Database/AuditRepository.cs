using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Database;

public sealed class AuditRepository : IAuditRepository
{
    private readonly DocsDbContext        _db;
    private readonly ILogger<AuditRepository> _log;

    public AuditRepository(DocsDbContext db, ILogger<AuditRepository> log)
    {
        _db  = db;
        _log = log;
    }

    public async Task InsertAsync(DocumentAudit audit, CancellationToken ct = default)
    {
        try
        {
            _db.DocumentAudits.Add(audit);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue; matches Node.js service behaviour
            _log.LogError(ex, "Failed to insert audit event {Event} for document {DocId}",
                audit.Event, audit.DocumentId);
        }
    }

    public async Task<IReadOnlyList<DocumentAudit>> ListForDocumentAsync(
        Guid documentId, Guid tenantId, int limit = 200, CancellationToken ct = default)
    {
        return await _db.DocumentAudits
            .Where(a => a.DocumentId == documentId && a.TenantId == tenantId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
