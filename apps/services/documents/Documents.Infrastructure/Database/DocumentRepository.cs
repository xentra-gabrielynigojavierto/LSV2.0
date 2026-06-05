using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Database;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly DocsDbContext _db;

    public DocumentRepository(DocsDbContext db) => _db = db;

    public async Task<Document?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        return await _db.Documents
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<Document> Items, int Total)> ListAsync(
        DocumentFilter filter, CancellationToken ct = default)
    {
        RequireTenantId(filter.TenantId);

        var query = _db.Documents.Where(d => d.TenantId == filter.TenantId);

        if (!string.IsNullOrWhiteSpace(filter.ProductId))
            query = query.Where(d => d.ProductId == filter.ProductId);
        if (!string.IsNullOrWhiteSpace(filter.ReferenceId))
            query = query.Where(d => d.ReferenceId == filter.ReferenceId);
        if (!string.IsNullOrWhiteSpace(filter.ReferenceType))
            query = query.Where(d => d.ReferenceType == filter.ReferenceType);
        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<DocumentStatus>(filter.Status, ignoreCase: true, out var status))
            query = query.Where(d => d.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Document> CreateAsync(Document document, CancellationToken ct = default)
    {
        RequireTenantId(document.TenantId);
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);
        return document;
    }

    public async Task<Document> UpdateAsync(Document document, CancellationToken ct = default)
    {
        RequireTenantId(document.TenantId);
        _db.Documents.Update(document);
        await _db.SaveChangesAsync(ct);
        return document;
    }

    public async Task SoftDeleteAsync(Guid id, Guid tenantId, Guid deletedBy, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        await _db.Documents
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.IsDeleted, true)
                .SetProperty(d => d.DeletedAt, DateTime.UtcNow)
                .SetProperty(d => d.DeletedBy, deletedBy)
                .SetProperty(d => d.Status, DocumentStatus.Deleted)
                .SetProperty(d => d.UpdatedAt, DateTime.UtcNow)
                .SetProperty(d => d.UpdatedBy, deletedBy),
            ct);
    }

    public async Task UpdateScanStatusAsync(Guid id, Guid tenantId, ScanStatusUpdate update, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        await _db.Documents
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ScanStatus,        update.ScanStatus)
                .SetProperty(d => d.ScanCompletedAt,   update.ScanCompletedAt)
                .SetProperty(d => d.ScanDurationMs,    update.ScanDurationMs)
                .SetProperty(d => d.ScanThreats,       update.ScanThreats)
                .SetProperty(d => d.ScanEngineVersion, update.ScanEngineVersion)
                .SetProperty(d => d.UpdatedAt,         DateTime.UtcNow),
            ct);
    }

    public async Task ClearPublishedLogoFlagAsync(Guid tenantId, CancellationToken ct = default)
    {
        RequireTenantId(tenantId);
        await _db.Documents
            .Where(d => d.TenantId == tenantId && d.IsPublishedAsLogo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.IsPublishedAsLogo, false)
                .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
            ct);
    }

    private static void RequireTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new Application.Exceptions.TenantIsolationException();
    }
}
