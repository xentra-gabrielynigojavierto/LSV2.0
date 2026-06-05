using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class NotificationService : INotificationService
{
    // How far ahead a reminder is scheduled before the appointment.
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(24);
    // Minimum gap: if the appointment is less than this away, schedule reminder ASAP (1 min from now).
    private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromHours(1);

    private readonly INotificationRepository _notifications;

    public NotificationService(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task<PagedResponse<NotificationResponse>> SearchAsync(
        Guid tenantId,
        GetNotificationsQuery query,
        CancellationToken ct = default)
    {
        ValidateQuery(query);

        var (items, total) = await _notifications.SearchAsync(tenantId, query, ct);

        return new PagedResponse<NotificationResponse>
        {
            Items      = items.Select(ToResponse).ToList(),
            Page       = query.Page,
            PageSize   = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<NotificationResponse> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Notification '{id}' was not found.");

        return ToResponse(notification);
    }

    public async Task CreateReferralStatusChangedAsync(
        Guid tenantId,
        Guid referralId,
        Guid? userId,
        CancellationToken ct = default)
    {
        var notification = CareConnectNotification.Create(
            tenantId:           tenantId,
            notificationType:   NotificationType.ReferralStatusChanged,
            relatedEntityType:  NotificationRelatedEntityType.Referral,
            relatedEntityId:    referralId,
            recipientType:      NotificationRecipientType.ClientEmail,
            recipientAddress:   null,
            subject:            "Your referral status has been updated.",
            message:            null,
            scheduledForUtc:    null,
            createdByUserId:    userId);

        await _notifications.AddAsync(notification, ct);
    }

    public async Task CreateAppointmentScheduledAsync(
        Guid tenantId,
        Guid appointmentId,
        DateTime scheduledStartUtc,
        Guid? userId,
        CancellationToken ct = default)
    {
        // Primary notification: appointment was just scheduled.
        var scheduled = CareConnectNotification.Create(
            tenantId:           tenantId,
            notificationType:   NotificationType.AppointmentScheduled,
            relatedEntityType:  NotificationRelatedEntityType.Appointment,
            relatedEntityId:    appointmentId,
            recipientType:      NotificationRecipientType.ClientEmail,
            recipientAddress:   null,
            subject:            "Your appointment has been scheduled.",
            message:            null,
            scheduledForUtc:    null,
            createdByUserId:    userId);

        // Reminder notification: 24 hours before the appointment.
        // If the appointment is < 25 hours away, fire as soon as possible (1 minute from now).
        var now = DateTime.UtcNow;
        var reminderTime = scheduledStartUtc - ReminderLeadTime;
        if (reminderTime < now + MinimumLeadTime)
            reminderTime = now + TimeSpan.FromMinutes(1);

        var reminder = CareConnectNotification.Create(
            tenantId:           tenantId,
            notificationType:   NotificationType.AppointmentReminder,
            relatedEntityType:  NotificationRelatedEntityType.Appointment,
            relatedEntityId:    appointmentId,
            recipientType:      NotificationRecipientType.ClientEmail,
            recipientAddress:   null,
            subject:            "Reminder: you have an upcoming appointment.",
            message:            null,
            scheduledForUtc:    reminderTime,
            createdByUserId:    userId);

        await _notifications.AddRangeAsync(new[] { scheduled, reminder }, ct);
    }

    public async Task CreateAppointmentConfirmedAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? userId,
        CancellationToken ct = default)
    {
        var notification = CareConnectNotification.Create(
            tenantId:           tenantId,
            notificationType:   NotificationType.AppointmentConfirmed,
            relatedEntityType:  NotificationRelatedEntityType.Appointment,
            relatedEntityId:    appointmentId,
            recipientType:      NotificationRecipientType.ClientEmail,
            recipientAddress:   null,
            subject:            "Your appointment has been confirmed.",
            message:            null,
            scheduledForUtc:    null,
            createdByUserId:    userId);

        await _notifications.AddAsync(notification, ct);
    }

    public async Task CreateAppointmentCancelledAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? userId,
        CancellationToken ct = default)
    {
        var notification = CareConnectNotification.Create(
            tenantId:           tenantId,
            notificationType:   NotificationType.AppointmentCancelled,
            relatedEntityType:  NotificationRelatedEntityType.Appointment,
            relatedEntityId:    appointmentId,
            recipientType:      NotificationRecipientType.ClientEmail,
            recipientAddress:   null,
            subject:            "Your appointment has been cancelled.",
            message:            null,
            scheduledForUtc:    null,
            createdByUserId:    userId);

        await _notifications.AddAsync(notification, ct);
    }

    private static void ValidateQuery(GetNotificationsQuery q)
    {
        var errors = new Dictionary<string, string[]>();

        if (q.Page < 1)
            errors["page"] = new[] { "Page must be >= 1." };

        if (q.PageSize < 1)
            errors["pageSize"] = new[] { "PageSize must be >= 1." };
        else if (q.PageSize > 100)
            errors["pageSize"] = new[] { "PageSize must be <= 100." };

        if (q.Status is not null && !NotificationStatus.IsValid(q.Status))
            errors["status"] = new[] { $"'{q.Status}' is not a valid status. Allowed: {string.Join(", ", NotificationStatus.All)}." };

        if (q.NotificationType is not null && !NotificationType.IsValid(q.NotificationType))
            errors["notificationType"] = new[] { $"'{q.NotificationType}' is not a valid notification type. Allowed: {string.Join(", ", NotificationType.All)}." };

        if (q.RelatedEntityType is not null && !NotificationRelatedEntityType.IsValid(q.RelatedEntityType))
            errors["relatedEntityType"] = new[] { $"'{q.RelatedEntityType}' is not a valid entity type. Allowed: {string.Join(", ", NotificationRelatedEntityType.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static NotificationResponse ToResponse(CareConnectNotification n) => new()
    {
        Id                = n.Id,
        NotificationType  = n.NotificationType,
        RelatedEntityType = n.RelatedEntityType,
        RelatedEntityId   = n.RelatedEntityId,
        RecipientType     = n.RecipientType,
        RecipientAddress  = n.RecipientAddress,
        Subject           = n.Subject,
        Message           = n.Message,
        Status            = n.Status,
        ScheduledForUtc   = n.ScheduledForUtc,
        SentAtUtc         = n.SentAtUtc,
        FailedAtUtc       = n.FailedAtUtc,
        FailureReason     = n.FailureReason,
        CreatedAtUtc      = n.CreatedAtUtc
    };
}
