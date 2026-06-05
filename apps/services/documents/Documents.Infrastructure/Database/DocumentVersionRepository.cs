using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Database;

public sealed class DocumentVersionRepository : IDocumentVersionRepository
{
    private readonly DocsDbContext _db;

    public DocumentVersionRepository(DocsDbContext db) => _db = db;

    public async Task<IReadOnlyList<DocumentVersion>> ListByDocumentAsync(
        Guid documentId, Guid tenantId, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        return await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.TenantId == tenantId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<DocumentVersion> CreateAsync(DocumentVersion version, CancellationToken ct = default)
    {
        RequireTenantId(version.TenantId);
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync(ct);
        return version;
    }

    public async Task UpdateScanStatusAsync(Guid versionId, Guid tenantId, ScanStatusUpdate update, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        await _db.DocumentVersions
            .Where(v => v.Id == versionId && v.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.ScanStatus, update.ScanStatus)
                .SetProperty(v => v.ScanCompletedAt, update.ScanCompletedAt)
                .SetProperty(v => v.ScanDurationMs, update.ScanDurationMs)
                .SetProperty(v => v.ScanThreats, update.ScanThreats)
                .SetProperty(v => v.ScanEngineVersion, update.ScanEngineVersion),
            ct);
    }

    private static void RequireTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new Application.Exceptions.TenantIsolationException();
    }
}
