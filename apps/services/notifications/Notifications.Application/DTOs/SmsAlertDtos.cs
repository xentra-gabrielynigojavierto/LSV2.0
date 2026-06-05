namespace Notifications.Application.DTOs;

// ── Query model ───────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Filter/page model for SMS operational alert list queries.
/// All fields are optional; omitting a field means "no filter on that dimension".
/// </summary>
public sealed class SmsAlertQuery
{
    /// <summary>Filter by alert lifecycle status. Null means all statuses.</summary>
    public string? Status { get; set; }

    /// <summary>Filter by severity: "warning" | "critical". Null means all.</summary>
    public string? Severity { get; set; }

    /// <summary>Filter by alert type code. Null means all types.</summary>
    public string? AlertType { get; set; }

    /// <summary>Filter by scoped tenant. Null means all tenants (platform-wide).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Filter by provider name. Null means all providers.</summary>
    public string? Provider { get; set; }

    /// <summary>Filter by provider config ID. Null means all configs.</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Inclusive UTC start of the CreatedAt filter window.</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive UTC end of the CreatedAt filter window.</summary>
    public DateTime? To { get; set; }

    /// <summary>Max records returned per page. Clamped to 1–200. Default 50.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>Zero-based offset for pagination.</summary>
    public int Offset { get; set; } = 0;
}

// ── Alert item ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: A single SMS operational alert record.
/// No credentials, phone numbers, or raw provider payloads are included.
/// </summary>
public sealed class SmsAlertDto
{
    public Guid Id { get; init; }

    // ── Classification ────────────────────────────────────────────────────────

    public string AlertType { get; init; } = string.Empty;
    public string Severity { get; init; } = "warning";

    // ── Scope ─────────────────────────────────────────────────────────────────

    public Guid? TenantId { get; init; }
    public string? Provider { get; init; }
    public Guid? ProviderConfigId { get; init; }

    // ── Threshold context ────────────────────────────────────────────────────

    public decimal MetricValue { get; init; }
    public decimal ThresholdValue { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime EvaluationWindowStart { get; init; }
    public DateTime EvaluationWindowEnd { get; init; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public string Status { get; init; } = "active";
    public int OccurrenceCount { get; init; }
    public DateTime FirstObservedAt { get; init; }
    public DateTime LastObservedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime? SuppressedUntil { get; init; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

// ── List / pagination response ────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Paginated SMS alert list response.
/// </summary>
public sealed class SmsAlertListResult
{
    public IReadOnlyList<SmsAlertDto> Items { get; init; } = Array.Empty<SmsAlertDto>();
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
}

// ── Summary ───────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Aggregate counts for active/resolved/suppressed alerts.
/// </summary>
public sealed class SmsAlertSummaryDto
{
    public int ActiveCount { get; init; }
    public int ResolvedCount { get; init; }
    public int SuppressedCount { get; init; }
    public int TotalCount { get; init; }

    public int CriticalActiveCount { get; init; }
    public int WarningActiveCount { get; init; }

    /// <summary>Active alert counts grouped by AlertType.</summary>
    public IReadOnlyDictionary<string, int> ActiveByType { get; init; }
        = new Dictionary<string, int>();
}

// ── Resolve request ───────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Request body for the POST /v1/admin/sms/alerts/{id}/resolve endpoint.
/// </summary>
public sealed class SmsAlertResolveRequest
{
    /// <summary>
    /// Optional free-text note documenting why the alert was resolved.
    /// Max 1000 characters.
    /// </summary>
    public string? ResolutionNote { get; set; }
}

// ── Suppress request ──────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Request body for the POST /v1/admin/sms/alerts/{id}/suppress endpoint.
/// </summary>
public sealed class SmsAlertSuppressRequest
{
    /// <summary>
    /// How many minutes to suppress re-alerting for this alert's condition.
    /// Minimum 1 minute, maximum 10080 minutes (7 days). Default 60.
    /// </summary>
    public int SuppressForMinutes { get; set; } = 60;
}

// ── Evaluate response ─────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-010: Response for the POST /v1/admin/sms/alerts/evaluate endpoint.
/// Returns a summary of what happened during one evaluation cycle.
/// </summary>
public sealed class SmsAlertEvaluationResult
{
    /// <summary>UTC start of the window evaluated against.</summary>
    public DateTime WindowStart { get; init; }

    /// <summary>UTC end of the window evaluated against.</summary>
    public DateTime WindowEnd { get; init; }

    /// <summary>Wall-clock duration of the evaluation in milliseconds.</summary>
    public int DurationMs { get; init; }

    /// <summary>Rule codes evaluated during this cycle.</summary>
    public IReadOnlyList<string> EvaluatedRules { get; init; } = Array.Empty<string>();

    /// <summary>Number of new active alerts created.</summary>
    public int AlertsCreated { get; init; }

    /// <summary>Number of existing active alerts updated (OccurrenceCount incremented).</summary>
    public int AlertsUpdated { get; init; }

    /// <summary>
    /// Number of conditions that were skipped because a suppressed or recently-resolved
    /// alert exists for the same scope within the cooldown window.
    /// </summary>
    public int AlertsSuppressed { get; init; }

    /// <summary>Total SMS attempts in the evaluation window (for context).</summary>
    public int AttemptsSampled { get; init; }
}
