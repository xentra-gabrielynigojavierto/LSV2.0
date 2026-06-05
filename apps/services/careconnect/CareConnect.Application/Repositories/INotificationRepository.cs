using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface INotificationRepository
{
    Task<CareConnectNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<CareConnectNotification> Items, int TotalCount)> SearchAsync(Guid tenantId, GetNotificationsQuery query, CancellationToken ct = default);
    Task AddAsync(CareConnectNotification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CareConnectNotification> notifications, CancellationToken ct = default);
    Task UpdateAsync(CareConnectNotification notification, CancellationToken ct = default);

    // LSCC-005-01: referral-scoped notification queries
    Task<CareConnectNotification?> GetLatestByReferralAsync(
        Guid tenantId,
        Guid referralId,
        string? notificationType = null,
        CancellationToken ct = default);

    Task<List<CareConnectNotification>> GetAllByReferralAsync(
        Guid tenantId,
        Guid referralId,
        CancellationToken ct = default);

    Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default);
    Task<bool> TryAddWithDedupeAsync(CareConnectNotification notification, CancellationToken ct = default);

    // LSCC-005-02: retry worker query
    /// <summary>
    /// Returns failed notifications that are due for automatic retry:
    /// Status=Failed, NextRetryAfterUtc &lt;= utcNow, AttemptCount &lt; maxAttempts.
    /// Capped at <paramref name="batchSize"/> to bound each worker pass.
    /// </summary>
    Task<List<CareConnectNotification>> GetRetryEligibleAsync(
        DateTime utcNow,
        int maxAttempts,
        int batchSize,
        CancellationToken ct = default);
}
