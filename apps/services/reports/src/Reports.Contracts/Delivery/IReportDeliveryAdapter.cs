namespace Reports.Contracts.Delivery;

public interface IReportDeliveryAdapter
{
    string MethodName { get; }

    Task<DeliveryResult> DeliverAsync(
        byte[] fileContent,
        string fileName,
        string contentType,
        string? deliveryConfigJson,
        CancellationToken ct = default);
}
