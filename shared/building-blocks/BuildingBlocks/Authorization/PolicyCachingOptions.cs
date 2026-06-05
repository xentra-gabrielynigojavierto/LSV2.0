namespace BuildingBlocks.Authorization;

public class PolicyCachingOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "InMemory";
    public int TtlSeconds { get; set; } = 60;
    public string KeyPrefix { get; set; } = "policy";
}
