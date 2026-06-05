using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public sealed class StoreReportRequest
{
    public string FileName { get; init; } = string.Empty;
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string MimeType { get; init; } = "application/octet-stream";
}

public sealed class StoredDocumentInfo
{
    public string DocumentId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset StoredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ReportContent
{
    public string DocumentId { get; init; } = string.Empty;
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string MimeType { get; init; } = "application/octet-stream";
}

public interface IDocumentAdapter
{
    Task<AdapterResult<StoredDocumentInfo>> StoreReportAsync(RequestContext ctx, TenantContext tenant, StoreReportRequest request, CancellationToken ct = default);
    Task<AdapterResult<ReportContent>> RetrieveReportAsync(RequestContext ctx, TenantContext tenant, string documentId, CancellationToken ct = default);
}
