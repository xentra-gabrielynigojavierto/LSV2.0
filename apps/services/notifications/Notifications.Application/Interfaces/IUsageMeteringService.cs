namespace Notifications.Application.Interfaces;

public class MeterEventInput
{
    public Guid TenantId { get; set; }
    public string UsageUnit { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? NotificationAttemptId { get; set; }
    public string? Provider { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? ProviderUnitCost { get; set; }
    public decimal? ProviderTotalCost { get; set; }
    public string? Currency { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public interface IUsageMeteringService
{
    Task MeterAsync(MeterEventInput input);
    Task MeterBatchAsync(IEnumerable<MeterEventInput> inputs);
}
