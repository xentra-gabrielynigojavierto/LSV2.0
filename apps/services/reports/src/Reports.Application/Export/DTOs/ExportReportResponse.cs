namespace Reports.Application.Export.DTOs;

public sealed class ExportReportResponse
{
    public Guid ExportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public ExportFormat Format { get; init; }
    public string Status { get; init; } = string.Empty;
    public byte[] FileContent { get; init; } = Array.Empty<byte>();
    public string? StorageKey { get; init; }
}
