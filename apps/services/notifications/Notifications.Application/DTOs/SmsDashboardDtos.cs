namespace Notifications.Application.DTOs;

// ── Query model ───────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: Shared filter model for all SMS dashboard admin endpoints.
/// All filters are optional. Dashboard queries always restrict to Channel = "sms".
/// </summary>
public sealed class SmsDashboardQuery
{
    /// <summary>
    /// Optional cross-tenant filter. Null means all tenants.
    /// Admin callers may omit to get platform-wide aggregates.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Filter by provider name (e.g. "twilio"). Case-insensitive.</summary>
    public string? Provider { get; set; }

    /// <summary>Filter by a specific tenant provider config (opaque identifier).</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Filter by ownership mode: "tenant" | "platform".</summary>
    public string? ProviderOwnershipMode { get; set; }

    /// <summary>Filter by attempt status (e.g. "sent", "delivered", "failed").</summary>
    public string? Status { get; set; }

    /// <summary>Filter by failure category code.</summary>
    public string? FailureCategory { get; set; }

    /// <summary>Inclusive start of the CreatedAt window (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive end of the CreatedAt window (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Time-series bucket size for the trends endpoint.
    /// Accepted values: "hour" | "day" | "week". Default: "day".
    /// Normalized to lowercase by endpoint layer before use.
    /// </summary>
    public string Bucket { get; set; } = "day";

    /// <summary>Max tenant groups returned by the tenant breakdown endpoint. Default 100.</summary>
    public int TenantBreakdownLimit { get; set; } = 100;

    /// <summary>Max provider rows returned by the provider breakdown endpoint. Default 200.</summary>
    public int ProviderBreakdownLimit { get; set; } = 200;

    /// <summary>Max failure rows returned by the failure breakdown endpoint. Default 50.</summary>
    public int FailureBreakdownLimit { get; set; } = 50;
}

// ── Summary ───────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: High-level SMS delivery and reconciliation KPI aggregate.
/// All counts reflect attempts matching the query filters.
/// No credentials, phone numbers, or raw provider payloads are included.
/// </summary>
public sealed class SmsDashboardSummaryDto
{
    // ── Total ──────────────────────────────────────────────────────────────────
    public int TotalAttempts { get; init; }

    // ── Delivery status counts ─────────────────────────────────────────────────
    public int SentCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int DeadLetterCount { get; init; }

    /// <summary>Status = "pending".</summary>
    public int PendingCount { get; init; }

    /// <summary>Status = "processing" or "queued".</summary>
    public int ProcessingCount { get; init; }

    /// <summary>Status = "sending".</summary>
    public int SendingCount { get; init; }

    /// <summary>Status = "retrying".</summary>
    public int RetryingCount { get; init; }

    // ── Provider attribution counts ────────────────────────────────────────────
    public int TenantOwnedCount { get; init; }
    public int PlatformOwnedCount { get; init; }
    public int UnknownOwnershipCount { get; init; }

    // ── Reconciliation KPIs (LS-NOTIF-SMS-007) ────────────────────────────────
    public int ReconciledTotal { get; init; }
    public int NeverReconciled { get; init; }
    public int ReconciliationUpdated { get; init; }
    public int ReconciliationNoChange { get; init; }
    public int ReconciliationLookupFailed { get; init; }
    public int ReconciliationSkipped { get; init; }
    public int ReconciliationProviderConfigFailed { get; init; }

    // ── Cardinality ───────────────────────────────────────────────────────────
    /// <summary>Number of distinct tenants with SMS attempt records in the window.</summary>
    public int UniqueTenantCount { get; init; }

    /// <summary>Number of distinct provider names used in the window.</summary>
    public int UniqueProviderCount { get; init; }

    /// <summary>Number of distinct ProviderConfigId values used in the window.</summary>
    public int UniqueProviderConfigCount { get; init; }

    // ── Window bounds ──────────────────────────────────────────────────────────
    /// <summary>Earliest CreatedAt in the result window. Null if no records.</summary>
    public DateTime? EarliestAt { get; init; }

    /// <summary>Latest CreatedAt in the result window. Null if no records.</summary>
    public DateTime? LatestAt { get; init; }
}

// ── Trends ────────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: A single time-series data point for the trends endpoint.
/// </summary>
public sealed class SmsDashboardTrendPointDto
{
    /// <summary>UTC start of the bucket (inclusive).</summary>
    public DateTime BucketStart { get; init; }

    /// <summary>UTC end of the bucket (inclusive, 1 tick before next bucket start).</summary>
    public DateTime BucketEnd { get; init; }

    public int TotalAttempts { get; init; }
    public int SentCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }

    /// <summary>
    /// Attempts created in this bucket that have been reconciled at least once.
    /// Based on CreatedAt bucket, not LastReconciledAt.
    /// </summary>
    public int ReconciledTotal { get; init; }

    /// <summary>Attempts created in this bucket whose last reconciliation outcome = vendor_lookup_failed.</summary>
    public int ReconciliationLookupFailed { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-008: Time-series trend response.
/// </summary>
public sealed class SmsDashboardTrendResult
{
    /// <summary>Normalized bucket size: "hour" | "day" | "week".</summary>
    public string Bucket { get; init; } = "day";

    /// <summary>Resolved start of the queried window (UTC).</summary>
    public DateTime WindowFrom { get; init; }

    /// <summary>Resolved end of the queried window (UTC).</summary>
    public DateTime WindowTo { get; init; }

    public IReadOnlyList<SmsDashboardTrendPointDto> Points { get; init; } = Array.Empty<SmsDashboardTrendPointDto>();
}

// ── Failure breakdown ─────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: A single failure category/error entry in the failure breakdown.
/// </summary>
public sealed class SmsDashboardFailureItemDto
{
    /// <summary>
    /// Delivery failure category (FailureCategory column).
    /// "unknown" when FailureCategory is null but Status is failed/dead_letter.
    /// </summary>
    public string FailureCategory { get; init; } = "unknown";

    /// <summary>
    /// Last reconciliation error code, if applicable. Null when not set.
    /// Safe string code only — no credentials or raw payloads.
    /// </summary>
    public string? ErrorCode { get; init; }

    public int Count { get; init; }

    /// <summary>Most recent UpdatedAt timestamp among matching attempts.</summary>
    public DateTime LatestOccurrenceAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-008: Failure breakdown response.
/// </summary>
public sealed class SmsDashboardFailureResult
{
    public IReadOnlyList<SmsDashboardFailureItemDto> Items { get; init; } = Array.Empty<SmsDashboardFailureItemDto>();
    public int TotalFailedAttempts { get; init; }
}

// ── Tenant breakdown ──────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: Per-tenant SMS activity aggregate.
/// Tenant names are not returned — the Notification Service has no local name store.
/// Control Center should enrich with names from Identity service.
/// </summary>
public sealed class SmsDashboardTenantItemDto
{
    /// <summary>Tenant identifier. Null for platform-owned sends with no tenant scope.</summary>
    public Guid? TenantId { get; init; }

    public int TotalAttempts { get; init; }
    public int SentCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
    public int ReconciledTotal { get; init; }
    public int NeverReconciled { get; init; }
    public int TenantOwnedCount { get; init; }
    public int PlatformOwnedCount { get; init; }

    /// <summary>Most recent CreatedAt timestamp for this tenant's attempts in the window.</summary>
    public DateTime LatestActivityAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-008: Tenant breakdown response.
/// </summary>
public sealed class SmsDashboardTenantResult
{
    public IReadOnlyList<SmsDashboardTenantItemDto> Items { get; init; } = Array.Empty<SmsDashboardTenantItemDto>();
    public int TotalTenants { get; init; }
}

// ── Provider breakdown ────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-008: Per-provider/config SMS activity aggregate.
/// No CredentialsJson, SettingsJson, or authToken is included.
/// ProviderConfigId is an opaque operational identifier.
/// </summary>
public sealed class SmsDashboardProviderItemDto
{
    /// <summary>Provider name (e.g. "twilio").</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Opaque provider config ID. Null for platform-default sends without a config record.
    /// </summary>
    public Guid? ProviderConfigId { get; init; }

    /// <summary>"tenant" | "platform" | "unknown".</summary>
    public string ProviderOwnershipMode { get; init; } = "unknown";

    public int TotalAttempts { get; init; }
    public int SentCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int ReconciledTotal { get; init; }
    public int ReconciliationLookupFailed { get; init; }

    /// <summary>Most recent CreatedAt timestamp for this provider group in the window.</summary>
    public DateTime LatestActivityAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-008: Provider breakdown response.
/// </summary>
public sealed class SmsDashboardProviderResult
{
    public IReadOnlyList<SmsDashboardProviderItemDto> Items { get; init; } = Array.Empty<SmsDashboardProviderItemDto>();
    public int TotalProviderConfigs { get; init; }
}
