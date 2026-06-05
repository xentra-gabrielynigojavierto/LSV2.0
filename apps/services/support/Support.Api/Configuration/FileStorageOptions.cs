namespace Support.Api.Configuration;

public enum FileStorageMode
{
    NoOp,
    Local,
    DocumentsService,
}

public class FileStorageOptions
{
    public const string SectionName = "Support:FileStorage";

    public FileStorageMode Mode { get; set; } = FileStorageMode.NoOp;

    /// <summary>Maximum file size accepted, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; } = 25;

    /// <summary>
    /// Allowed MIME types. If empty, the upload endpoint rejects all uploads
    /// (an explicit allowlist is required).
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    /// <summary>Root directory for the Local provider. Created if missing.</summary>
    public string LocalRootPath { get; set; } = "./data/support-uploads";

    public DocumentsServiceOptions DocumentsService { get; set; } = new();
}

public class DocumentsServiceOptions
{
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    /// <summary>
    /// Endpoint path appended to BaseUrl. Defaults to the convention used by the
    /// LegalSynq Documents Service. Override in config if the service exposes a
    /// different path.
    /// </summary>
    public string UploadPath { get; set; } = "/documents/api/documents/upload";
}
