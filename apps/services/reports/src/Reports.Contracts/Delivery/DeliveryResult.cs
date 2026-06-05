namespace Reports.Contracts.Delivery;

public sealed class DeliveryResult
{
    public bool Success { get; init; }
    public string Method { get; init; } = string.Empty;
    public string? Message { get; init; }
    public DateTimeOffset DeliveredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? DetailJson { get; init; }
    public string? ExternalReferenceId { get; init; }
    public long? DurationMs { get; init; }
    public bool IsRetryable { get; init; }
}
