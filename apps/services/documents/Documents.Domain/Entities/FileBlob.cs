namespace Documents.Domain.Entities;

public class FileBlob
{
    public string StorageKey { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string MimeType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
