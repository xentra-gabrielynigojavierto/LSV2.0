namespace Task.Application.DTOs;

// ── SLA Analytics ─────────────────────────────────────────────────────────────

/// <summary>
/// Pre-computed SLA analytics for a single tenant, returned by the internal
/// Task analytics endpoint consumed by Flow's FlowAnalyticsService.
/// </summary>
public record TaskSlaAnalyticsResponse(
    IReadOnlyList<SlaStatusCountDto>  SlaGroups,
    double?                           AvgOverdueAgeDays,
    int                               BreachedInWindow,
    int                               CompletedInWindow,
    int                               CompletedOnTimeInWindow,
    IReadOnlyList<QueueOverdueDto>    RoleOverdueGroups,
    IReadOnlyList<QueueOverdueDto>    OrgOverdueGroups);

public record SlaStatusCountDto(string SlaStatus, int Count);

public record QueueOverdueDto(string QueueKey, int OverdueCount);

// ── Queue Analytics ────────────────────────────────────────────────────────────

/// <summary>
/// Pre-computed queue and workload analytics for a single tenant.
/// </summary>
public record TaskQueueAnalyticsResponse(
    IReadOnlyList<QueueGroupCountDto> RoleGroups,
    IReadOnlyList<QueueGroupCountDto> OrgGroups,
    int                               UnassignedCount,
    double?                           OldestQueueAgeHours,
    double?                           MedianQueueAgeHours,
    int                               ActiveUserCount,
    int                               OverloadedUserCount);

public record QueueGroupCountDto(string Key, string Status, string SlaStatus, int Count);

// ── Assignment Analytics ───────────────────────────────────────────────────────

/// <summary>
/// Pre-computed assignment analytics for a single tenant.
/// </summary>
public record TaskAssignmentAnalyticsResponse(
    IReadOnlyList<ModeCountDto>       ModeGroups,
    int                               AssignedInWindow,
    IReadOnlyList<UserStatusCountDto> UserStatusGroups);

public record ModeCountDto(string? Mode, int Count);

public record UserStatusCountDto(string UserId, string Status, int Count);

// ── Platform Summary (cross-tenant) ────────────────────────────────────────────

/// <summary>
/// Cross-tenant active task and SLA summary returned by the platform analytics
/// endpoint. Consumed by Flow's GetPlatformSummaryAsync (admin only).
/// </summary>
public record TaskPlatformAnalyticsResponse(
    int                               TotalActiveTasks,
    int                               TotalOverdueTasks,
    IReadOnlyList<TenantSlaCountDto>  TenantSlaGroups);

public record TenantSlaCountDto(Guid TenantId, string SlaStatus, int Count);
