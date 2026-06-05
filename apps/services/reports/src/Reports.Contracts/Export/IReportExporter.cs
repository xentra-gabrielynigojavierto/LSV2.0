using Reports.Contracts.Adapters;

namespace Reports.Contracts.Export;

public sealed class ExportContext
{
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ExportResult
{
    public byte[] FileContent { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
}

public interface IReportExporter
{
    string FormatName { get; }
    Task<ExportResult> ExportAsync(TabularResultSet data, ExportContext ctx, CancellationToken ct = default);
}
