using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Reports.Contracts.Configuration;
using Reports.Contracts.Delivery;

namespace Reports.Infrastructure.Adapters;

public sealed class RealSftpReportDeliveryAdapter : IReportDeliveryAdapter
{
    private readonly SftpDeliverySettings _settings;
    private readonly ILogger<RealSftpReportDeliveryAdapter> _log;

    public string MethodName => "SFTP";

    public RealSftpReportDeliveryAdapter(
        IOptions<SftpDeliverySettings> settings,
        ILogger<RealSftpReportDeliveryAdapter> log)
    {
        _settings = settings.Value;
        _log = log;
    }

    public async Task<DeliveryResult> DeliverAsync(
        byte[] fileContent, string fileName, string contentType,
        string? deliveryConfigJson, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        string host = _settings.Host;
        int port = _settings.Port;
        string remotePath = _settings.RemotePath;
        string username = _settings.Username;

        if (!string.IsNullOrWhiteSpace(deliveryConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deliveryConfigJson);
                if (doc.RootElement.TryGetProperty("host", out var h) && !string.IsNullOrEmpty(h.GetString()))
                    host = h.GetString()!;
                if (doc.RootElement.TryGetProperty("port", out var p) && p.TryGetInt32(out var pVal))
                    port = pVal;
                if (doc.RootElement.TryGetProperty("path", out var pa) && !string.IsNullOrEmpty(pa.GetString()))
                    remotePath = pa.GetString()!;
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex, "SFTP delivery: failed to parse deliveryConfigJson");
            }
        }

        var fullRemotePath = $"{remotePath.TrimEnd('/')}/{fileName}";
        int attempt = 0;
        int maxAttempts = Math.Max(1, _settings.MaxRetries + 1);
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var authMethods = BuildAuthMethods(username);
                var connectionInfo = new ConnectionInfo(host, port, username, authMethods)
                {
                    Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds),
                };

                await Task.Run(() =>
                {
                    using var client = new SftpClient(connectionInfo);
                    client.Connect();

                    EnsureDirectoryExists(client, remotePath);

                    using var stream = new MemoryStream(fileContent);
                    client.UploadFile(stream, fullRemotePath, true);

                    client.Disconnect();
                }, ct);

                sw.Stop();

                _log.LogInformation(
                    "SFTP delivery success: file={FileName} host={Host} path={RemotePath} size={Size} durationMs={DurationMs}",
                    fileName, host, fullRemotePath, fileContent.Length, sw.ElapsedMilliseconds);

                return new DeliveryResult
                {
                    Success = true,
                    Method = MethodName,
                    Message = $"SFTP uploaded to {host}:{fullRemotePath}.",
                    DeliveredAtUtc = DateTimeOffset.UtcNow,
                    ExternalReferenceId = fullRemotePath,
                    DurationMs = sw.ElapsedMilliseconds,
                    DetailJson = JsonSerializer.Serialize(new { host, port, remotePath = fullRemotePath, fileName, fileSize = fileContent.Length }),
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _log.LogWarning(ex, "SFTP delivery attempt {Attempt} failed: file={FileName} host={Host}", attempt, fileName, host);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(1000 * attempt, ct);
                    continue;
                }

                break;
            }
        }

        sw.Stop();
        var isRetryable = lastException is not (Renci.SshNet.Common.SshAuthenticationException or InvalidOperationException);
        return new DeliveryResult
        {
            Success = false,
            Method = MethodName,
            Message = $"SFTP delivery failed after {attempt} attempt(s): {lastException?.Message}",
            DeliveredAtUtc = DateTimeOffset.UtcNow,
            ExternalReferenceId = fullRemotePath,
            DurationMs = sw.ElapsedMilliseconds,
            IsRetryable = isRetryable,
            DetailJson = JsonSerializer.Serialize(new { host, port, remotePath = fullRemotePath, fileName, error = lastException?.Message, attempts = attempt }),
        };
    }

    private AuthenticationMethod[] BuildAuthMethods(string username)
    {
        var methods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(_settings.PrivateKeyPath) && File.Exists(_settings.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(_settings.PrivateKeyPassphrase)
                ? new PrivateKeyFile(_settings.PrivateKeyPath)
                : new PrivateKeyFile(_settings.PrivateKeyPath, _settings.PrivateKeyPassphrase);
            methods.Add(new PrivateKeyAuthenticationMethod(username, keyFile));
        }

        if (!string.IsNullOrEmpty(_settings.Password))
        {
            methods.Add(new PasswordAuthenticationMethod(username, _settings.Password));
        }

        if (methods.Count == 0)
            throw new InvalidOperationException("SFTP: No authentication method configured (password or private key required).");

        return methods.ToArray();
    }

    private static void EnsureDirectoryExists(SftpClient client, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var segment in segments)
        {
            current += $"/{segment}";
            try
            {
                if (!client.Exists(current))
                    client.CreateDirectory(current);
            }
            catch
            {
            }
        }
    }
}
