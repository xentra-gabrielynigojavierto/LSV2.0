namespace Notifications.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════════
// Policy DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// LS-NOTIF-SMS-011: A single escalation policy record as returned by admin APIs.
/// Target is always masked — the raw webhook URL or email is never exposed.
/// </summary>
public sealed class SmsEscalationPolicyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }

    // Matching criteria
    public string? AlertType { get; init; }
    public string? Severity { get; init; }
    public Guid? TenantId { get; init; }
    public string? Provider { get; init; }
    public Guid? ProviderConfigId { get; init; }

    // Channel
    public string ChannelType { get; init; } = string.Empty;

    /// <summary>Masked target (never contains the raw URL or full email).</summary>
    public string TargetMasked { get; init; } = string.Empty;

    /// <summary>Safe display label for the target, if configured.</summary>
    public string? TargetDisplay { get; init; }

    // Dedup + retry
    public int CooldownMinutes { get; init; }
    public bool RetryEnabled { get; init; }
    public int MaxRetryCount { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-011: Paginated policy list response.
/// </summary>
public sealed class SmsEscalationPolicyListResult
{
    public IReadOnlyList<SmsEscalationPolicyDto> Items { get; init; } = Array.Empty<SmsEscalationPolicyDto>();
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-011: Query/filter model for escalation policy listing.
/// </summary>
public sealed class SmsEscalationPolicyQuery
{
    public bool? Enabled { get; set; }
    public string? ChannelType { get; set; }
    public string? AlertType { get; set; }
    public string? Severity { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// LS-NOTIF-SMS-011: Request body for creating an escalation policy.
/// </summary>
public sealed class CreateSmsEscalationPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public string? AlertType { get; set; }
    public string? Severity { get; set; }
    public Guid? TenantId { get; set; }
    public string? Provider { get; set; }
    public Guid? ProviderConfigId { get; set; }

    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// The raw delivery target (webhook URL or email address).
    /// Required. Stored securely; never returned in full by any API.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Safe display label for the target (optional).</summary>
    public string? TargetDisplay { get; set; }

    public int CooldownMinutes { get; set; } = 60;
    public bool RetryEnabled { get; set; }
    public int MaxRetryCount { get; set; } = 3;
}

/// <summary>
/// LS-NOTIF-SMS-011: Request body for updating an escalation policy.
/// </summary>
public sealed class UpdateSmsEscalationPolicyRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }

    public string? AlertType { get; set; }
    public string? Severity { get; set; }
    public Guid? TenantId { get; set; }
    public string? Provider { get; set; }
    public Guid? ProviderConfigId { get; set; }

    public string? ChannelType { get; set; }

    /// <summary>New raw target. If null, the existing target is preserved.</summary>
    public string? Target { get; set; }

    public string? TargetDisplay { get; set; }
    public int? CooldownMinutes { get; set; }
    public bool? RetryEnabled { get; set; }
    public int? MaxRetryCount { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Escalation Attempt DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// LS-NOTIF-SMS-011: A single escalation attempt record as returned by admin APIs.
/// TargetMasked is the only target field — raw URL/email is never returned.
/// </summary>
public sealed class SmsAlertEscalationDto
{
    public Guid Id { get; init; }
    public Guid AlertId { get; init; }
    public Guid? PolicyId { get; init; }
    public string ChannelType { get; init; } = string.Empty;
    public string? TargetMasked { get; init; }
    public string Severity { get; init; } = "warning";
    public string Status { get; init; } = "pending";
    public int AttemptCount { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public DateTime? SentAt { get; init; }
    public string? FailureReason { get; init; }
    public DateTime? NextRetryAt { get; init; }
    public DateTime? SuppressedUntil { get; init; }
    public string? PayloadHash { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-011: Paginated escalation history list response.
/// </summary>
public sealed class SmsAlertEscalationListResult
{
    public IReadOnlyList<SmsAlertEscalationDto> Items { get; init; } = Array.Empty<SmsAlertEscalationDto>();
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-011: Query/filter model for escalation history listing.
/// </summary>
public sealed class SmsAlertEscalationQuery
{
    public Guid? AlertId { get; set; }
    public Guid? PolicyId { get; set; }
    public string? Status { get; set; }
    public string? ChannelType { get; set; }
    public string? Severity { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// LS-NOTIF-SMS-011: Aggregate summary of escalation outcomes.
/// </summary>
public sealed class SmsEscalationSummaryDto
{
    public int TotalCount { get; init; }
    public int SentCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
    public int SuppressedCount { get; init; }
    public int SkippedCount { get; init; }

    /// <summary>Counts grouped by channel type.</summary>
    public IReadOnlyDictionary<string, int> ByChannel { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Counts grouped by status.</summary>
    public IReadOnlyDictionary<string, int> ByStatus { get; init; }
        = new Dictionary<string, int>();
}
