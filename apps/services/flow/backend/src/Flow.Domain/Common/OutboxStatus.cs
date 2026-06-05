namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E10.2 — string-valued status constants for
/// <see cref="Flow.Domain.Entities.OutboxMessage"/>. Stored as
/// <c>varchar(16)</c> for human readability in operator queries.
/// </summary>
public static class OutboxStatus
{
    public const string Pending      = "Pending";
    public const string Processing   = "Processing";
    public const string Succeeded    = "Succeeded";
    public const string Failed       = "Failed";
    public const string DeadLettered = "DeadLettered";
}
