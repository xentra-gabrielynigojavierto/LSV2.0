namespace Reports.Contracts.Context;

public sealed class RequestContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string RequestId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string>? Metadata { get; init; }

    public static RequestContext Default() => new();
}
