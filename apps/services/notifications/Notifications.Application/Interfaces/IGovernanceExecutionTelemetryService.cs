namespace Notifications.Application.Interfaces;

// ---------------------------------------------------------------------------
// Query / result types
// ---------------------------------------------------------------------------

public sealed class GovernanceExecutionQuery
{
    public string?   ChannelType   { get; set; }
    public Guid?     TenantId      { get; set; }
    public string?   DecisionType  { get; set; }
    public bool?     IsSimulation  { get; set; }
    public DateTime? From          { get; set; }
    public DateTime? To            { get; set; }
    public int       Page          { get; set; } = 1;
    public int       PageSize      { get; set; } = 50;
}

public sealed class GovernanceExecutionRecordDto
{
    public Guid     Id                      { get; set; }
    public Guid?    NotificationId          { get; set; }
    public Guid?    AttemptId               { get; set; }
    public Guid?    TenantId                { get; set; }
    public string   ChannelType             { get; set; } = string.Empty;
    public string   DecisionType            { get; set; } = string.Empty;
    public string   ReasonCode              { get; set; } = string.Empty;
    public string?  ContentClassification   { get; set; }
    public string?  TopologyResolutionStatus { get; set; }
    public string?  EngineStatus            { get; set; }
    public bool     IsSimulation            { get; set; }
    public DateTime CreatedAt               { get; set; }
}

public sealed class GovernanceExecutionPageResult
{
    public IReadOnlyList<GovernanceExecutionRecordDto> Items { get; set; } = Array.Empty<GovernanceExecutionRecordDto>();
    public int TotalCount  { get; set; }
    public int Page        { get; set; }
    public int PageSize    { get; set; }
    public int TotalPages  { get; set; }
}

public sealed class GovernanceRuntimeTelemetryQuery
{
    public string?   ChannelType   { get; set; }
    public Guid?     TenantId      { get; set; }
    public bool?     IsSimulation  { get; set; }
    public DateTime? From          { get; set; }
    public DateTime? To            { get; set; }
}

public sealed class GovernanceChannelTelemetry
{
    public string ChannelType     { get; set; } = string.Empty;
    public long   TotalExecutions { get; set; }
    public long   AllowCount      { get; set; }
    public long   WarnCount       { get; set; }
    public long   BlockCount      { get; set; }
    public long   ReviewCount     { get; set; }
    public long   SuppressCount   { get; set; }
    public long   LiveCount        { get; set; }
    public long   SimulationCount  { get; set; }
    public long   TopologyFailures { get; set; }
    public long   EngineFailures   { get; set; }
}

public sealed class GovernanceRuntimeTelemetryResult
{
    public long   TotalExecutions       { get; set; }
    public long   LiveExecutions        { get; set; }
    public long   SimulationExecutions  { get; set; }
    public long   AllowCount            { get; set; }
    public long   WarnCount             { get; set; }
    public long   BlockCount            { get; set; }
    public long   ReviewCount           { get; set; }
    public long   SuppressCount         { get; set; }
    public long   TopologyFailureCount  { get; set; }
    public long   EngineFailureCount    { get; set; }
    public IReadOnlyList<GovernanceChannelTelemetry> ByChannel { get; set; } = Array.Empty<GovernanceChannelTelemetry>();
    public DateTime? OldestRecord       { get; set; }
    public DateTime? NewestRecord       { get; set; }
}

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IGovernanceExecutionTelemetryService
{
    /// <summary>
    /// Persist a safe telemetry record for a completed governance execution.
    /// Payload text is NEVER persisted. Failure is non-fatal — logs and returns.
    /// </summary>
    Task RecordExecutionAsync(
        GovernanceExecutionContext context,
        GovernanceExecutionResult result,
        bool isSimulation,
        CancellationToken ct = default);

    /// <summary>Query paginated governance execution records.</summary>
    Task<GovernanceExecutionPageResult> QueryExecutionsAsync(
        GovernanceExecutionQuery query,
        CancellationToken ct = default);

    /// <summary>Get aggregate runtime telemetry.</summary>
    Task<GovernanceRuntimeTelemetryResult> GetRuntimeTelemetryAsync(
        GovernanceRuntimeTelemetryQuery query,
        CancellationToken ct = default);

    /// <summary>Get per-channel runtime status from telemetry data.</summary>
    Task<IReadOnlyList<GovernanceChannelTelemetry>> GetChannelStatusAsync(
        CancellationToken ct = default);
}
