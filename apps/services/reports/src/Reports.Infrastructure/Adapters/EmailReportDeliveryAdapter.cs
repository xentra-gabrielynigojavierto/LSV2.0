using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Delivery;

namespace Reports.Infrastructure.Adapters;

public sealed class EmailReportDeliveryAdapter : IReportDeliveryAdapter
{
    private readonly ILogger<EmailReportDeliveryAdapter> _log;

    public string MethodName => "Email";

    public EmailReportDeliveryAdapter(ILogger<EmailReportDeliveryAdapter> log) => _log = log;

    public Task<DeliveryResult> DeliverAsync(
        byte[] fileContent, string fileName, string contentType,
        string? deliveryConfigJson, CancellationToken ct)
    {
        string recipients = "unknown";
        if (!string.IsNullOrWhiteSpace(deliveryConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deliveryConfigJson);
                if (doc.RootElement.TryGetProperty("recipients", out var r))
                    recipients = r.GetString() ?? "unknown";
            }
            catch
            {
            }
        }

        _log.LogInformation(
            "Email delivery (mock): file={FileName} size={Size} recipients={Recipients}",
            fileName, fileContent.Length, recipients);

        return Task.FromResult(new DeliveryResult
        {
            Success = true,
            Method = MethodName,
            Message = $"Email delivered (mock) to {recipients} with attachment {fileName}.",
            DeliveredAtUtc = DateTimeOffset.UtcNow,
            DetailJson = JsonSerializer.Serialize(new { recipients, fileName, fileSize = fileContent.Length })
        });
    }
}
