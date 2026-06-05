using Support.Api.Files;

namespace Support.Tests;

/// <summary>
/// Test-only storage provider: records every upload request and returns a
/// canned <see cref="SupportFileUploadResult"/>. Can be flipped into failure
/// modes to simulate the NotConfigured / Remote-failure paths.
/// </summary>
public sealed class RecordingFileStorageProvider : ISupportFileStorageProvider
{
    private readonly object _gate = new();
    private readonly List<RecordedUpload> _calls = new();

    public bool ThrowNotConfigured { get; set; }
    public bool ThrowRemote { get; set; }

    /// <summary>Override the document id returned by UploadAsync (default: per-call guid).</summary>
    public string? DocumentIdOverride { get; set; }

    public Task<SupportFileUploadResult> UploadAsync(
        SupportFileUploadRequest request,
        CancellationToken ct = default)
    {
        if (ThrowNotConfigured) throw new SupportFileStorageNotConfiguredException();
        if (ThrowRemote)
            throw new SupportFileStorageRemoteException("Simulated upstream failure");

        // Drain the stream so we can assert size and ensure the service really
        // handed us bytes (not just a metadata stub).
        long bytes;
        using (var ms = new MemoryStream())
        {
            request.Stream.CopyTo(ms);
            bytes = ms.Length;
        }

        var docId = DocumentIdOverride ?? $"recorded-{Guid.NewGuid():N}";
        var result = new SupportFileUploadResult(
            DocumentId: docId,
            FileName: request.FileName,
            ContentType: request.ContentType,
            FileSizeBytes: bytes,
            StorageProvider: "recording",
            StorageUri: null);

        lock (_gate)
        {
            _calls.Add(new RecordedUpload(request.TenantId, request.TicketId,
                request.FileName, request.ContentType, bytes,
                request.UploadedByUserId, result.DocumentId));
        }

        return Task.FromResult(result);
    }

    public IReadOnlyList<RecordedUpload> Calls
    {
        get { lock (_gate) return _calls.ToList(); }
    }
}

public sealed record RecordedUpload(
    string TenantId,
    Guid TicketId,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    string? UploadedByUserId,
    string DocumentId);
