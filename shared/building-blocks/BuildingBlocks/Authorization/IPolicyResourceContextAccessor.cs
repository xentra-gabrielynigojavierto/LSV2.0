namespace BuildingBlocks.Authorization;

public interface IPolicyResourceContextAccessor
{
    Dictionary<string, object?> GetResourceContext();
    void SetResourceContext(Dictionary<string, object?> context);
    void MergeResourceContext(string key, object? value);
}
