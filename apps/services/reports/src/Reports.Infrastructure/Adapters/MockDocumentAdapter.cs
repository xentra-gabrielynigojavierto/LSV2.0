using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockDocumentAdapter : IDocumentAdapter
{
    private readonly ILogger<MockDocumentAdapter> _log;

    public MockDocumentAdapter(ILogger<MockDocumentAdapter> log) => _log = log;

    public Task<AdapterResult<StoredDocumentInfo>> StoreReportAsync(RequestContext ctx, TenantContext tenant, StoreReportRequest request, CancellationToken ct)
    {
        _log.LogDebug("MockDocumentAdapter: StoreReport {FileName} ({Bytes} bytes) [Correlation={CorrelationId}]",
            request.FileName, request.Content.Length, ctx.CorrelationId);
        var info = new StoredDocumentInfo
        {
            DocumentId = $"mock-doc-{Guid.NewGuid():N}",
            FileName = request.FileName,
            SizeBytes = request.Content.Length,
        };
        return Task.FromResult(AdapterResult<StoredDocumentInfo>.Ok(info));
    }

    public Task<AdapterResult<ReportContent>> RetrieveReportAsync(RequestContext ctx, TenantContext tenant, string documentId, CancellationToken ct)
    {
        _log.LogDebug("MockDocumentAdapter: RetrieveReport {DocumentId} for tenant {TenantId} [Correlation={CorrelationId}]",
            documentId, tenant.TenantId, ctx.CorrelationId);
        return Task.FromResult(AdapterErrors.NotFoundResult<ReportContent>($"Document {documentId} not found (mock)"));
    }
}
