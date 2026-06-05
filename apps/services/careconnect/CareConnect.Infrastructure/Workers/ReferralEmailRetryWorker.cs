using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Workers;

/// <summary>
/// LSCC-005-02: Background worker that automatically retries failed referral-related emails
/// according to <see cref="ReferralRetryPolicy"/>.
///
/// Mechanism: IHostedService (BackgroundService) that wakes every 60 seconds, queries for
/// retry-eligible notifications (Status=Failed, NextRetryAfterUtc &lt;= now, AttemptCount &lt; MaxAttempts),
/// resolves the referral + provider, and re-attempts the SMTP send.
///
/// Safety guarantees:
/// - Processes notifications one at a time; no parallel retries for the same notification.
/// - Each iteration creates its own DI scope (IServiceScopeFactory) to get fresh DbContext instances.
/// - Only retries Failed notifications — Sent notifications are never re-processed.
/// - Respects MaxAttempts; does not retry beyond the configured limit.
/// - If the referral is no longer in New status, the retry is skipped and the notification
///   is marked as exhausted (NextRetryAfterUtc cleared) to prevent stale retries.
///
/// Resend vs Retry:
/// - This worker only processes notifications where TriggerSource != ManualResend.
/// - A successful manual resend suppresses future retries of the original failed notification
///   by clearing its NextRetryAfterUtc via ClearRetrySchedule() in ReferralService.ResendEmailAsync.
/// </summary>
public sealed class ReferralEmailRetryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<ReferralEmailRetryWorker> _logger;

    public ReferralEmailRetryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReferralEmailRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ReferralEmailRetryWorker starting. Poll interval: {Interval}s, MaxAttempts: {Max}.",
            PollInterval.TotalSeconds, ReferralRetryPolicy.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetryBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ReferralEmailRetryWorker: unhandled error in retry pass. Will retry next interval.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessRetryBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var notifRepo    = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var referralRepo = scope.ServiceProvider.GetRequiredService<IReferralRepository>();
        var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IReferralEmailService>();

        var utcNow = DateTime.UtcNow;
        var candidates = await notifRepo.GetRetryEligibleAsync(
            utcNow, ReferralRetryPolicy.MaxAttempts, BatchSize, ct);

        if (candidates.Count == 0) return;

        _logger.LogInformation(
            "ReferralEmailRetryWorker: {Count} notification(s) eligible for retry.", candidates.Count);

        foreach (var notification in candidates)
        {
            if (ct.IsCancellationRequested) break;
            await ProcessSingleRetryAsync(notification, notifRepo, referralRepo, providerRepo, emailService, ct);
        }
    }

    private async Task ProcessSingleRetryAsync(
        CareConnectNotification notification,
        INotificationRepository notifRepo,
        IReferralRepository     referralRepo,
        IProviderRepository     providerRepo,
        IReferralEmailService   emailService,
        CancellationToken       ct)
    {
        try
        {
            var referral = await referralRepo.GetByIdAsync(notification.TenantId, notification.RelatedEntityId, ct);
            if (referral is null)
            {
                _logger.LogWarning(
                    "RetryWorker: referral {ReferralId} not found for notification {NotifId}. Clearing retry schedule.",
                    notification.RelatedEntityId, notification.Id);
                notification.ClearRetrySchedule();
                await notifRepo.UpdateAsync(notification, ct);
                return;
            }

            if (!IsNotificationStillRelevant(notification.NotificationType, referral.Status))
            {
                _logger.LogInformation(
                    "RetryWorker: referral {ReferralId} is in status '{Status}'. " +
                    "Notification type '{NotifType}' is no longer relevant. Suppressing retry for {NotifId}.",
                    referral.Id, referral.Status, notification.NotificationType, notification.Id);
                notification.ClearRetrySchedule();
                await notifRepo.UpdateAsync(notification, ct);
                return;
            }

            var provider = await providerRepo.GetByIdCrossAsync(referral.ProviderId, ct);
            if (provider is null)
            {
                _logger.LogWarning(
                    "RetryWorker: provider {ProviderId} not found for referral {ReferralId}. Clearing retry.",
                    referral.ProviderId, referral.Id);
                notification.ClearRetrySchedule();
                await notifRepo.UpdateAsync(notification, ct);
                return;
            }

            _logger.LogInformation(
                "RetryWorker: retrying notification {NotifId} (attempt {Attempt}/{Max}) for referral {ReferralId}.",
                notification.Id, notification.AttemptCount + 1, ReferralRetryPolicy.MaxAttempts, referral.Id);

            await emailService.RetryNotificationAsync(notification, referral, provider, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "RetryWorker: unexpected error processing notification {NotifId}. Skipping this iteration.",
                notification.Id);
        }
    }

    private static bool IsNotificationStillRelevant(string notificationType, string referralStatus)
    {
        return notificationType switch
        {
            NotificationType.ReferralCreated or
            NotificationType.ReferralEmailAutoRetry or
            NotificationType.ReferralEmailResent
                => referralStatus is "New" or "NewOpened",

            NotificationType.ReferralAcceptedProvider or
            NotificationType.ReferralAcceptedReferrer or
            NotificationType.ReferralAcceptedClient
                => referralStatus == "Accepted",

            NotificationType.ReferralRejectedProvider or
            NotificationType.ReferralRejectedReferrer
                => referralStatus == "Declined",

            NotificationType.ReferralCancelledProvider or
            NotificationType.ReferralCancelledReferrer
                => referralStatus == "Cancelled",

            _ => false,
        };
    }
}
