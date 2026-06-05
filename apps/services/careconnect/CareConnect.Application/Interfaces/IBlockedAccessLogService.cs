namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-01-004: Best-effort blocked-access event logger.
///
/// Implementations must never throw — failures are swallowed with a warning log.
/// Callers must not await the result when fire-and-forget semantics are preferred.
/// </summary>
public interface IBlockedAccessLogService
{
    /// <summary>
    /// Records a failed provider access-readiness check.
    /// Best-effort — never blocks or throws on failure.
    /// </summary>
    Task LogAsync(
        Guid?   tenantId,
        Guid?   userId,
        string? userEmail,
        Guid?   organizationId,
        Guid?   providerId,
        Guid?   referralId,
        string  failureReason,
        CancellationToken ct = default);
}
