using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Delivery;

namespace Reports.Infrastructure.Adapters;

public sealed class SftpReportDeliveryAdapter : IReportDeliveryAdapter
{
    private readonly ILogger<SftpReportDeliveryAdapter> _log;

    public string MethodName => "SFTP";

    public SftpReportDeliveryAdapter(ILogger<SftpReportDeliveryAdapter> log) => _log = log;

    public Task<DeliveryResult> DeliverAsync(
        byte[] fileContent, string fileName, string contentType,
        string? deliveryConfigJson, CancellationToken ct)
    {
        string host = "unknown";
        string path = "/";
        if (!string.IsNullOrWhiteSpace(deliveryConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deliveryConfigJson);
                if (doc.RootElement.TryGetProperty("host", out var h))
                    host = h.GetString() ?? "unknown";
                if (doc.RootElement.TryGetProperty("path", out var p))
                    path = p.GetString() ?? "/";
            }
            catch
            {
            }
        }

        _log.LogInformation(
            "SFTP delivery (stub): file={FileName} size={Size} host={Host} path={Path}",
            fileName, fileContent.Length, host, path);

        return Task.FromResult(new DeliveryResult
        {
            Success = true,
            Method = MethodName,
            Message = $"SFTP upload (stub) to {host}:{path}/{fileName}.",
            DeliveredAtUtc = DateTimeOffset.UtcNow,
            DetailJson = JsonSerializer.Serialize(new { host, path, fileName, fileSize = fileContent.Length })
        });
    }
}
