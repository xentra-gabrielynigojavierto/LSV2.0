namespace BuildingBlocks.Authorization;

public class PolicyVersioningOptions
{
    public string Provider { get; set; } = "InMemory";
    public string Scope { get; set; } = "Global";
}
