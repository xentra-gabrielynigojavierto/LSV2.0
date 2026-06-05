namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — configuration for the shared <see cref="IFlowClient"/>.
/// Bound from the <c>Flow</c> configuration section in each product API.
/// </summary>
public sealed class FlowClientOptions
{
    public const string SectionName = "Flow";

    /// <summary>
    /// Base URL of the Flow service (e.g. <c>http://localhost:5012</c>). Required.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Per-request timeout. Defaults to 10 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
