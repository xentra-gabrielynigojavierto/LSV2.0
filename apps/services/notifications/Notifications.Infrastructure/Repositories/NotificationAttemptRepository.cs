using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class NotificationAttemptRepository : INotificationAttemptRepository
{
    private readonly NotificationsDbContext _db;
    public NotificationAttemptRepository(NotificationsDbContext db) => _db = db;

    public async Task<NotificationAttempt?> GetByIdAsync(Guid id)
        => await _db.NotificationAttempts.FindAsync(id);

    public async Task<NotificationAttempt?> FindByProviderMessageIdAsync(string providerMessageId)
        => await _db.NotificationAttempts.FirstOrDefaultAsync(a => a.ProviderMessageId == providerMessageId);

    public async Task<List<NotificationAttempt>> GetByNotificationIdAsync(Guid notificationId)
        => await _db.NotificationAttempts.Where(a => a.NotificationId == notificationId)
            .OrderBy(a => a.AttemptNumber).ToListAsync();

    public async Task<NotificationAttempt> CreateAsync(NotificationAttempt attempt)
    {
        attempt.Id = attempt.Id == Guid.Empty ? Guid.NewGuid() : attempt.Id;
        attempt.CreatedAt = DateTime.UtcNow;
        attempt.UpdatedAt = DateTime.UtcNow;
        _db.NotificationAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        return attempt;
    }

    public async Task UpdateAsync(NotificationAttempt attempt)
    {
        attempt.UpdatedAt = DateTime.UtcNow;
        _db.NotificationAttempts.Update(attempt);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime? completedAt = null)
    {
        var a = await _db.NotificationAttempts.FindAsync(id);
        if (a == null) return;
        a.Status = status;
        if (completedAt.HasValue) a.CompletedAt = completedAt;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<NotificationAttempt>> GetStaleSmsAttemptsAsync(
        int limit,
        DateTime olderThan,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct = default)
        => await _db.NotificationAttempts
            .Where(a =>
                a.Channel == "sms" &&
                a.ProviderMessageId != null &&
                a.ProviderMessageId != "" &&
                statuses.Contains(a.Status) &&
                a.UpdatedAt < olderThan)
            .OrderBy(a => a.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task UpdateCostAsync(
        Guid attemptId,
        decimal? estimatedCostAmount,
        decimal? actualCostAmount,
        string? costCurrency,
        string costSource,
        DateTime costRecordedAt,
        CancellationToken ct = default)
    {
        var a = await _db.NotificationAttempts.FindAsync(new object[] { attemptId }, ct);
        if (a == null) return;

        a.EstimatedCostAmount = estimatedCostAmount;
        a.ActualCostAmount    = actualCostAmount;
        a.CostCurrency        = costCurrency;
        a.CostSource          = costSource;
        a.CostRecordedAt      = costRecordedAt;
        a.UpdatedAt           = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateReconciliationTrackingAsync(
        Guid attemptId,
        string outcome,
        string? errorCode,
        string? providerStatus,
        string? normalizedStatus,
        DateTime reconciledAt,
        CancellationToken ct = default)
    {
        var a = await _db.NotificationAttempts.FindAsync(new object[] { attemptId }, ct);
        if (a == null) return;

        a.LastReconciliationOutcome         = outcome;
        a.LastReconciledAt                  = reconciledAt;
        a.LastReconciliationErrorCode       = errorCode;
        a.LastReconciliationProviderStatus  = providerStatus;
        a.LastReconciliationNormalizedStatus = normalizedStatus;
        a.ReconciliationAttemptCount++;
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
