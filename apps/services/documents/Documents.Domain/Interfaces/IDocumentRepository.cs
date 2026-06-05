using Documents.Domain.Entities;

namespace Documents.Domain.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Document> Items, int Total)> ListAsync(DocumentFilter filter, CancellationToken ct = default);
    Task<Document> CreateAsync(Document document, CancellationToken ct = default);
    Task<Document> UpdateAsync(Document document, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid tenantId, Guid deletedBy, CancellationToken ct = default);
    Task UpdateScanStatusAsync(Guid id, Guid tenantId, ScanStatusUpdate update, CancellationToken ct = default);
    Task ClearPublishedLogoFlagAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class DocumentFilter
{
    public Guid    TenantId      { get; init; }
    public string? ProductId     { get; init; }
    public string? ReferenceId   { get; init; }
    public string? ReferenceType { get; init; }
    public string? Status        { get; init; }
    public int     Limit         { get; init; } = 50;
    public int     Offset        { get; init; } = 0;
}
