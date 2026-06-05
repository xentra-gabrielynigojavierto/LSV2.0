using Amazon.S3;
using Amazon.S3.Model;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public string BucketName       { get; set; } = "docs-local";
    public string Region           { get; set; } = "us-east-1";
    public string? AccessKeyId     { get; set; }
    public string? SecretAccessKey { get; set; }
}

public sealed class S3StorageProvider : IStorageProvider, IAsyncDisposable
{
    private readonly IAmazonS3            _s3;
    private readonly S3StorageOptions     _opts;
    private readonly ILogger<S3StorageProvider> _log;

    public string ProviderName => "s3";

    public S3StorageProvider(IOptions<S3StorageOptions> opts, ILogger<S3StorageProvider> log)
    {
        _opts = opts.Value;
        _log  = log;

        var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_opts.Region) };

        _s3 = _opts.AccessKeyId is not null
            ? new AmazonS3Client(_opts.AccessKeyId, _opts.SecretAccessKey, config)
            : new AmazonS3Client(config);  // uses instance role / env credentials
    }

    public async Task<string> UploadAsync(string key, Stream content, string mimeType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName  = _opts.BucketName,
            Key         = key,
            InputStream = content,
            ContentType = mimeType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

        await _s3.PutObjectAsync(request, ct);
        _log.LogDebug("S3: uploaded {Key} to {Bucket}", key, _opts.BucketName);
        return _opts.BucketName;
    }

    public async Task<string> GenerateSignedUrlAsync(string key, int ttlSeconds, string disposition, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key        = key,
            Expires    = DateTime.UtcNow.AddSeconds(ttlSeconds),
            Protocol   = Protocol.HTTPS,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = disposition == "download"
                    ? $"attachment; filename=\"{Path.GetFileName(key)}\""
                    : "inline",
            },
        };

        var url = _s3.GetPreSignedURL(request);
        return await Task.FromResult(url);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_opts.BucketName, key, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var response = await _s3.GetObjectAsync(_opts.BucketName, key, ct);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(_opts.BucketName, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _s3.Dispose();
        await Task.CompletedTask;
    }
}
