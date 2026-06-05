using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class DeliveryStatusService : IDeliveryStatusService
{
    private readonly INotificationAttemptRepository _attemptRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly ILogger<DeliveryStatusService> _logger;

    private static readonly HashSet<string> AttemptTerminal = new() { "delivered", "failed" };
    private static readonly HashSet<string> NotificationTerminal = new() { "sent", "failed" };

    private static readonly Dictionary<string, string> NormalizedToAttemptStatus = new()
    {
        ["accepted"] = "sending", ["queued"] = "sending", ["sent"] = "sent",
        ["delivered"] = "sent", ["failed"] = "failed", ["undeliverable"] = "failed",
        ["bounced"] = "failed", ["rejected"] = "failed"
    };

    private static readonly Dictionary<string, string> NormalizedToNotificationStatus = new()
    {
        ["accepted"] = "processing", ["queued"] = "processing", ["deferred"] = "processing",
        ["sent"] = "sent", ["delivered"] = "sent", ["failed"] = "failed",
        ["undeliverable"] = "failed", ["bounced"] = "failed", ["rejected"] = "failed"
    };

    public DeliveryStatusService(INotificationAttemptRepository attemptRepo, INotificationRepository notificationRepo, ILogger<DeliveryStatusService> logger)
    {
        _attemptRepo = attemptRepo;
        _notificationRepo = notificationRepo;
        _logger = logger;
    }

    public async Task UpdateAttemptFromEventAsync(Guid attemptId, string normalizedEventType)
    {
        var attempt = await _attemptRepo.GetByIdAsync(attemptId);
        if (attempt == null) return;
        if (AttemptTerminal.Contains(attempt.Status))
        {
            _logger.LogDebug("Skipping attempt status update - already terminal: {AttemptId} {Current} {Event}", attemptId, attempt.Status, normalizedEventType);
            return;
        }
        if (!NormalizedToAttemptStatus.TryGetValue(normalizedEventType, out var newStatus)) return;
        var completedAt = newStatus is "failed" or "sent" ? DateTime.UtcNow : (DateTime?)null;
        await _attemptRepo.UpdateStatusAsync(attemptId, newStatus, completedAt);
        _logger.LogDebug("Updated attempt status: {AttemptId} -> {NewStatus} from {Event}", attemptId, newStatus, normalizedEventType);
    }

    public async Task UpdateNotificationFromEventAsync(Guid notificationId, string normalizedEventType)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null) return;
        if (NotificationTerminal.Contains(notification.Status))
        {
            var isFinalFailure = normalizedEventType is "bounced" or "undeliverable" or "rejected";
            if (notification.Status == "failed") return;
            if (notification.Status == "sent" && !isFinalFailure) return;
        }
        if (!NormalizedToNotificationStatus.TryGetValue(normalizedEventType, out var newStatus)) return;
        await _notificationRepo.UpdateStatusAsync(notificationId, newStatus);
        _logger.LogDebug("Updated notification status: {NotificationId} -> {NewStatus} from {Event}", notificationId, newStatus, normalizedEventType);
    }
}
