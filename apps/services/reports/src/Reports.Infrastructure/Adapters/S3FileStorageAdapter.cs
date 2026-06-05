using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reports.Contracts.Configuration;
using Reports.Contracts.Storage;

namespace Reports.Infrastructure.Adapters;

public sealed class S3FileStorageAdapter : IFileStorageAdapter
{
    private readonly StorageSettings _settings;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorageAdapter> _log;

    public bool IsEnabled => _settings.Enabled;

    public S3FileStorageAdapter(
        IOptions<StorageSettings> settings,
        IAmazonS3 s3Client,
        ILogger<S3FileStorageAdapter> log)
    {
        _settings = settings.Value;
        _s3Client = s3Client;
        _log = log;
    }

    public async Task<FileStorageResult> UploadAsync(FileStorageRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var key = BuildKey(request);

        var putRequest = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            ContentType = request.ContentType,
            InputStream = new MemoryStream(request.Content),
            Metadata =
            {
                ["x-amz-meta-tenant-id"] = request.TenantId,
                ["x-amz-meta-original-filename"] = request.FileName,
            },
        };

        await _s3Client.PutObjectAsync(putRequest, ct);

        sw.Stop();

        var url = $"s3://{_settings.BucketName}/{key}";

        _log.LogInformation(
            "S3 upload success: key={StorageKey} bucket={BucketName} size={Size} durationMs={DurationMs}",
            key, _settings.BucketName, request.Content.Length, sw.ElapsedMilliseconds);

        return new FileStorageResult
        {
            StorageKey = key,
            Url = url,
            Provider = "S3",
            BucketName = _settings.BucketName,
            SizeBytes = request.Content.Length,
            StoredAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private string BuildKey(FileStorageRequest request)
    {
        var basePath = _settings.BasePath.Trim('/');
        var datePath = DateTimeOffset.UtcNow.ToString("yyyy/MM/dd");
        var subPath = string.IsNullOrEmpty(request.SubPath) ? "" : $"/{request.SubPath.Trim('/')}";
        return $"{basePath}/{request.TenantId}/{datePath}{subPath}/{request.FileName}";
    }
}
