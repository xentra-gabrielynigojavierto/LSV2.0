namespace Reports.Contracts.Context;

public sealed class ProductContext
{
    public string ProductCode { get; init; } = string.Empty;
    public string? ProductName { get; init; }
}
