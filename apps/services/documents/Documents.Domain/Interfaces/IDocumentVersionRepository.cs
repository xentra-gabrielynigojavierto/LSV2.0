using Documents.Domain.Entities;

namespace Documents.Domain.Interfaces;

public interface IDocumentVersionRepository
{
    Task<IReadOnlyList<DocumentVersion>> ListByDocumentAsync(Guid documentId, Guid tenantId, CancellationToken ct = default);
    Task<DocumentVersion> CreateAsync(DocumentVersion version, CancellationToken ct = default);
    Task UpdateScanStatusAsync(Guid versionId, Guid tenantId, Entities.ScanStatusUpdate update, CancellationToken ct = default);
}
