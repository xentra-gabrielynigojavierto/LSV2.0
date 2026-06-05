namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// A tenant ranked by event count. Only returned to platform-admin callers.
/// </summary>
public sealed class AuditTopTenantItem
{
    /// <summary>Tenant identifier.</summary>
    public required string TenantId { get; init; }

    /// <summary>Total events generated within this tenant in the query scope.</summary>
    public required long Count { get; init; }
}
