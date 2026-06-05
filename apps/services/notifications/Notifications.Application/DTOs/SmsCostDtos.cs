namespace Notifications.Application.DTOs;

// ── Query model ───────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-013: Filter model for all SMS cost analytics admin endpoints.
/// All filters are optional. Cost queries always restrict to Channel = "sms".
/// No credentials, phone numbers, or raw provider payloads are returned.
/// </summary>
public sealed class SmsCostQuery
{
    /// <summary>Optional cross-tenant filter. Null means platform-wide.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Provider name filter (e.g. "twilio"). Case-insensitive.</summary>
    public string? Provider { get; set; }

    /// <summary>Filter by specific tenant provider config (opaque Guid).</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Filter by ownership mode: "tenant" | "platform".</summary>
    public string? ProviderOwnershipMode { get; set; }

    /// <summary>Filter by attempt status (e.g. "sent", "delivered", "failed").</summary>
    public string? Status { get; set; }

    /// <summary>Filter by failure category.</summary>
    public string? FailureCategory { get; set; }

    /// <summary>Filter by cost source: "estimated" | "provider_reconciled" | "manual" | "unavailable".</summary>
    public string? CostSource { get; set; }

    /// <summary>Filter by ISO 4217 currency code (e.g. "USD").</summary>
    public string? Currency { get; set; }

    /// <summary>Inclusive start of the CreatedAt window (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive end of the CreatedAt window (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>Time-series bucket: "hour" | "day" | "week". Default: "day".</summary>
    public string Bucket { get; set; } = "day";

    /// <summary>Max provider rows in breakdown. Default 200.</summary>
    public int ProviderBreakdownLimit { get; set; } = 200;

    /// <summary>Max tenant rows in breakdown. Default 100.</summary>
    public int TenantBreakdownLimit { get; set; } = 100;

    /// <summary>Max failure rows in breakdown. Default 50.</summary>
    public int FailureBreakdownLimit { get; set; } = 50;

    /// <summary>Max rows for export. Default 5000.</summary>
    public int ExportLimit { get; set; } = 5000;
}

// ── Summary ───────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-013: Platform-wide SMS cost KPI aggregate.
/// EffectiveCost = ActualCostAmount ?? EstimatedCostAmount.
/// No credentials, phone numbers, or raw provider payloads.
/// </summary>
public sealed class SmsCostSummaryDto
{
    // ── Attempt counts ─────────────────────────────────────────────────────────
    public int TotalAttempts { get; init; }
    public int CostedAttempts { get; init; }
    public int UncostedAttempts { get; init; }

    // ── Cost totals ────────────────────────────────────────────────────────────
    /// <summary>Sum of effective cost (ActualCostAmount ?? EstimatedCostAmount) across all costed attempts.</summary>
    public decimal TotalEffectiveCost { get; init; }
    public decimal TotalEstimatedCost { get; init; }
    public decimal TotalActualCost { get; init; }

    // ── Cost by delivery outcome ───────────────────────────────────────────────
    public decimal DeliveredCost { get; init; }
    public decimal SentCost { get; init; }
    public decimal FailedCost { get; init; }
    public decimal DeadLetterCost { get; init; }
    public decimal RetryCost { get; init; }

    // ── Cost by ownership ─────────────────────────────────────────────────────
    public decimal TenantOwnedCost { get; init; }
    public decimal PlatformOwnedCost { get; init; }

    // ── Derived metrics ────────────────────────────────────────────────────────
    /// <summary>Effective cost per delivered message. Null when DeliveredCount = 0.</summary>
    public decimal? CostPerDeliveredMessage { get; init; }

    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }

    // ── Currency ───────────────────────────────────────────────────────────────
    public string Currency { get; init; } = "USD";

    // ── Cost source breakdown ──────────────────────────────────────────────────
    public int EstimatedCostCount { get; init; }
    public int ProviderReconciledCount { get; init; }
    public int UnavailableCount { get; init; }

    // ── Window bounds ──────────────────────────────────────────────────────────
    public DateTime? EarliestAt { get; init; }
    public DateTime? LatestAt { get; init; }
}

// ── Trends ────────────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-013: Single time-series cost data point.</summary>
public sealed class SmsCostTrendPointDto
{
    public DateTime BucketStart { get; init; }
    public DateTime BucketEnd { get; init; }
    public int TotalAttempts { get; init; }
    public int CostedAttempts { get; init; }
    public decimal TotalEffectiveCost { get; init; }
    public decimal DeliveredCost { get; init; }
    public decimal FailedCost { get; init; }
    public decimal RetryCost { get; init; }
    public string Currency { get; init; } = "USD";
}

/// <summary>LS-NOTIF-SMS-013: Cost trend response.</summary>
public sealed class SmsCostTrendResult
{
    public string Bucket { get; init; } = "day";
    public DateTime WindowFrom { get; init; }
    public DateTime WindowTo { get; init; }
    public IReadOnlyList<SmsCostTrendPointDto> Points { get; init; } = Array.Empty<SmsCostTrendPointDto>();
    public string Currency { get; init; } = "USD";
}

// ── Provider breakdown ────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-013: Per-provider/config cost aggregate. No credentials or settings.</summary>
public sealed class SmsCostProviderItemDto
{
    public string Provider { get; init; } = string.Empty;
    public Guid? ProviderConfigId { get; init; }
    public string ProviderOwnershipMode { get; init; } = "unknown";
    public int TotalAttempts { get; init; }
    public int DeliveredAttempts { get; init; }
    public int FailedAttempts { get; init; }
    public int CostedAttempts { get; init; }
    public decimal TotalEffectiveCost { get; init; }
    public decimal? CostPerDeliveredMessage { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime? LatestActivityAt { get; init; }
}

/// <summary>LS-NOTIF-SMS-013: Provider cost breakdown response.</summary>
public sealed class SmsCostProviderResult
{
    public IReadOnlyList<SmsCostProviderItemDto> Items { get; init; } = Array.Empty<SmsCostProviderItemDto>();
    public int TotalProviderConfigs { get; init; }
    public decimal GrandTotalEffectiveCost { get; init; }
    public string Currency { get; init; } = "USD";
}

// ── Tenant breakdown ──────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-013: Per-tenant SMS cost aggregate.
/// TenantId is the only identifier returned — tenant names must be enriched from Identity.
/// </summary>
public sealed class SmsCostTenantItemDto
{
    public Guid? TenantId { get; init; }
    public int TotalAttempts { get; init; }
    public int DeliveredAttempts { get; init; }
    public int FailedAttempts { get; init; }
    public int CostedAttempts { get; init; }
    public decimal TotalEffectiveCost { get; init; }
    public decimal? CostPerDeliveredMessage { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime? LatestActivityAt { get; init; }
}

/// <summary>LS-NOTIF-SMS-013: Tenant cost breakdown response.</summary>
public sealed class SmsCostTenantResult
{
    public IReadOnlyList<SmsCostTenantItemDto> Items { get; init; } = Array.Empty<SmsCostTenantItemDto>();
    public int TotalTenants { get; init; }
    public decimal GrandTotalEffectiveCost { get; init; }
    public string Currency { get; init; } = "USD";
}

// ── Failure / retry cost breakdown ────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-013: Failure category cost aggregate.</summary>
public sealed class SmsCostFailureItemDto
{
    public string FailureCategory { get; init; } = "unknown";
    public bool IsRetry { get; init; }
    public int Count { get; init; }
    public int CostedCount { get; init; }
    public decimal TotalEffectiveCost { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime? LatestOccurrenceAt { get; init; }
}

/// <summary>LS-NOTIF-SMS-013: Failure/retry cost breakdown response.</summary>
public sealed class SmsCostFailureResult
{
    public IReadOnlyList<SmsCostFailureItemDto> Items { get; init; } = Array.Empty<SmsCostFailureItemDto>();
    public int TotalFailedAttempts { get; init; }
    public decimal TotalFailedCost { get; init; }
    public decimal TotalRetryCost { get; init; }
    public string Currency { get; init; } = "USD";
}

// ── Export ────────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-013: Export-ready row. One row per SMS attempt.
/// No credentials, raw provider payloads, or phone numbers.
/// </summary>
public sealed class SmsCostExportRowDto
{
    public Guid AttemptId { get; init; }
    public Guid NotificationId { get; init; }
    public Guid? TenantId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public Guid? ProviderConfigId { get; init; }
    public string? ProviderOwnershipMode { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? FailureCategory { get; init; }
    public int AttemptNumber { get; init; }
    public bool IsRetry { get; init; }
    public decimal? EstimatedCostAmount { get; init; }
    public decimal? ActualCostAmount { get; init; }
    public decimal? EffectiveCostAmount { get; init; }
    public string? CostCurrency { get; init; }
    public string? CostSource { get; init; }
    public DateTime? CostRecordedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>LS-NOTIF-SMS-013: Export response wrapper.</summary>
public sealed class SmsCostExportResult
{
    public IReadOnlyList<SmsCostExportRowDto> Rows { get; init; } = Array.Empty<SmsCostExportRowDto>();
    public int TotalRows { get; init; }
    public bool Truncated { get; init; }
    public int Limit { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
