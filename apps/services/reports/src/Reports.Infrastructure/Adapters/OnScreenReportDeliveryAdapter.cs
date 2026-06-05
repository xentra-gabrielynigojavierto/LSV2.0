using Reports.Contracts.Delivery;

namespace Reports.Infrastructure.Adapters;

public sealed class OnScreenReportDeliveryAdapter : IReportDeliveryAdapter
{
    public string MethodName => "OnScreen";

    public Task<DeliveryResult> DeliverAsync(
        byte[] fileContent, string fileName, string contentType,
        string? deliveryConfigJson, CancellationToken ct)
    {
        return Task.FromResult(new DeliveryResult
        {
            Success = true,
            Method = MethodName,
            Message = "Report generated and ready for on-screen download.",
            DeliveredAtUtc = DateTimeOffset.UtcNow
        });
    }
}
