using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace CareConnect.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentSlotRepository _slots;
    private readonly IAppointmentRepository _appointments;
    private readonly IAppointmentStatusHistoryRepository _history;
    private readonly IReferralRepository _referrals;
    private readonly INotificationService _notifications;
    private readonly IAuditEventClient _auditClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppointmentService(
        IAppointmentSlotRepository slots,
        IAppointmentRepository appointments,
        IAppointmentStatusHistoryRepository history,
        IReferralRepository referrals,
        INotificationService notifications,
        IAuditEventClient auditClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _slots               = slots;
        _appointments        = appointments;
        _history             = history;
        _referrals           = referrals;
        _notifications       = notifications;
        _auditClient         = auditClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResponse<SlotResponse>> SearchSlotsAsync(
        Guid tenantId,
        SlotSearchParams query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Min(100, Math.Max(1, query.PageSize ?? 20));

        var (items, total) = await _slots.SearchAsync(
            tenantId,
            query.ProviderId,
            query.FacilityId,
            query.ServiceOfferingId,
            query.From,
            query.To,
            query.Status,
            page,
            pageSize,
            ct);

        return new PagedResponse<SlotResponse>
        {
            Items = items.Select(ToSlotResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<AppointmentResponse> CreateAppointmentAsync(
        Guid tenantId,
        Guid? userId,
        CreateAppointmentRequest request,
        CancellationToken ct = default,
        string? actorName = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ReferralId == Guid.Empty)
            errors["referralId"] = new[] { "ReferralId is required." };

        if (request.AppointmentSlotId == Guid.Empty)
            errors["appointmentSlotId"] = new[] { "AppointmentSlotId is required." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        var referral = await _referrals.GetByIdAsync(tenantId, request.ReferralId, ct)
            ?? throw new NotFoundException($"Referral '{request.ReferralId}' was not found.");

        var slot = await _slots.GetByIdAsync(tenantId, request.AppointmentSlotId, ct)
            ?? throw new NotFoundException($"Appointment slot '{request.AppointmentSlotId}' was not found.");

        if (slot.Status != SlotStatus.Open)
            throw new ConflictException(
                "Slot is not available for booking.",
                "SLOT_CONFLICT");

        if (slot.ReservedCount >= slot.Capacity)
            throw new ConflictException(
                "Slot has no remaining capacity.",
                "SLOT_CONFLICT");

        slot.Reserve(userId);

        // LSCC-002: Denormalize org participant IDs from referral so appointment queries
        // can be org-scoped without joining back to Referral for every read.
        var appointment = Appointment.Create(
            tenantId,
            request.ReferralId,
            slot.ProviderId,
            slot.FacilityId,
            slot.ServiceOfferingId,
            slot.Id,
            slot.StartAtUtc,
            slot.EndAtUtc,
            request.Notes,
            userId,
            organizationRelationshipId: referral.OrganizationRelationshipId,
            referringOrganizationId: referral.ReferringOrganizationId,
            receivingOrganizationId: referral.ReceivingOrganizationId);

        await _appointments.SaveBookingAsync(slot, appointment, ct);

        // Canonical audit: careconnect.appointment.scheduled — fire-and-observe, never gates booking.
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.appointment.scheduled",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "appointment-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId?.ToString(),
                Type = userId.HasValue ? ActorType.User : ActorType.System,
                Name = actorName ?? userId?.ToString() ?? "(system)",
            },
            Entity  = new AuditEventEntityDto { Type = "Appointment", Id = appointment.Id.ToString() },
            Action  = "AppointmentScheduled",
            Description = $"Appointment scheduled for referral '{request.ReferralId}' on slot '{request.AppointmentSlotId}'.",
            Outcome = "success",
            Metadata = JsonSerializer.Serialize(new
            {
                appointmentId = appointment.Id,
                referralId    = request.ReferralId,
                slotId        = slot.Id,
                providerId    = slot.ProviderId,
                facilityId    = slot.FacilityId,
                startAtUtc    = slot.StartAtUtc,
                tenantId,
            }),
            CorrelationId  = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
            RequestId      = _httpContextAccessor.HttpContext?.TraceIdentifier,
            IdempotencyKey = IdempotencyKey.For("care-connect", "careconnect.appointment.scheduled", appointment.Id.ToString()),
            Tags = ["appointment", "scheduled"],
        });

        // Notification hook: AppointmentScheduled + AppointmentReminder.
        try { await _notifications.CreateAppointmentScheduledAsync(tenantId, appointment.Id, slot.StartAtUtc, userId, ct); }
        catch { /* Notification failure must not break the booking. */ }

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    // LSCC-002: referringOrgId/receivingOrgId forwarded for org-participant scoping
    public async Task<PagedResponse<AppointmentResponse>> SearchAppointmentsAsync(
        Guid tenantId,
        AppointmentSearchParams query,
        Guid? referringOrgId = null,
        Guid? receivingOrgId = null,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Min(100, Math.Max(1, query.PageSize ?? 20));

        var statusFilter = query.Status;
        if (string.Equals(statusFilter, "Pending", StringComparison.OrdinalIgnoreCase))
            statusFilter = "Scheduled";

        var (items, total) = await _appointments.SearchAsync(
            tenantId,
            query.ReferralId,
            query.ProviderId,
            statusFilter,
            query.From,
            query.To,
            page,
            pageSize,
            referringOrgId: referringOrgId,
            receivingOrgId: receivingOrgId,
            ct: ct);

        return new PagedResponse<AppointmentResponse>
        {
            Items = items.Select(ToAppointmentResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<AppointmentResponse> GetAppointmentByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> UpdateAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        AppointmentStatusHistory? history = null;

        if (request.Status is not null && request.Status != appointment.Status)
        {
            AppointmentWorkflowRules.ValidateStatus(request.Status);
            AppointmentWorkflowRules.ValidateTransition(appointment.Status, request.Status);

            var oldStatus = appointment.Status;
            appointment.UpdateStatus(request.Status, userId);

            history = AppointmentStatusHistory.Create(
                appointment.Id,
                tenantId,
                oldStatus,
                request.Status,
                userId,
                request.Notes);
        }

        if (request.Notes is not null)
            appointment.UpdateNotes(request.Notes, userId);

        await _appointments.SaveStatusUpdateAsync(appointment, history, ct);

        // Notification hook: fire when status becomes Confirmed.
        if (request.Status == AppointmentStatus.Confirmed)
        {
            try { await _notifications.CreateAppointmentConfirmedAsync(tenantId, appointment.Id, userId, ct); }
            catch { /* Notification failure must not break the update. */ }
        }

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> ConfirmAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        ConfirmAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        AppointmentWorkflowRules.ValidateTransition(appointment.Status, AppointmentStatus.Confirmed);

        var oldStatus = appointment.Status;
        appointment.UpdateStatus(AppointmentStatus.Confirmed, userId);

        var history = AppointmentStatusHistory.Create(
            appointment.Id,
            tenantId,
            oldStatus,
            AppointmentStatus.Confirmed,
            userId,
            request.Notes);

        await _appointments.SaveStatusUpdateAsync(appointment, history, ct);

        try { await _notifications.CreateAppointmentConfirmedAsync(tenantId, appointment.Id, userId, ct); }
        catch { }

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> CompleteAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        CompleteAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        AppointmentWorkflowRules.ValidateTransition(appointment.Status, AppointmentStatus.Completed);

        var oldStatus = appointment.Status;
        appointment.UpdateStatus(AppointmentStatus.Completed, userId);

        var history = AppointmentStatusHistory.Create(
            appointment.Id,
            tenantId,
            oldStatus,
            AppointmentStatus.Completed,
            userId,
            request.Notes);

        await _appointments.SaveStatusUpdateAsync(appointment, history, ct);

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> CancelAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        CancelAppointmentRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        if (AppointmentWorkflowRules.IsTerminal(appointment.Status))
            throw new ConflictException(
                $"Cannot cancel an appointment with terminal status '{appointment.Status}'.",
                "INVALID_STATE_TRANSITION");

        AppointmentWorkflowRules.ValidateTransition(appointment.Status, AppointmentStatus.Cancelled);

        AppointmentSlot? slot = null;
        if (appointment.AppointmentSlotId.HasValue)
        {
            slot = await _slots.GetByIdAsync(tenantId, appointment.AppointmentSlotId.Value, ct);
            slot?.Release(userId);
        }

        var oldStatus = appointment.Status;
        appointment.UpdateStatus(AppointmentStatus.Cancelled, userId);

        var history = AppointmentStatusHistory.Create(
            appointment.Id,
            tenantId,
            oldStatus,
            AppointmentStatus.Cancelled,
            userId,
            request.Notes);

        await _appointments.SaveCancellationAsync(appointment, slot, history, ct);

        // Notification hook: AppointmentCancelled.
        try { await _notifications.CreateAppointmentCancelledAsync(tenantId, appointment.Id, userId, ct); }
        catch { /* Notification failure must not break the cancellation. */ }

        // Canonical audit: careconnect.appointment.cancelled — fire-and-observe.
        var auditNow = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.appointment.cancelled",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "appointment-service",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = auditNow,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = tenantId.ToString() },
            Actor = new AuditEventActorDto
            {
                Id   = userId?.ToString(),
                Type = userId.HasValue ? ActorType.User : ActorType.System,
            },
            Entity = new AuditEventEntityDto { Type = "Appointment", Id = appointment.Id.ToString() },
            Action      = "AppointmentCancelled",
            Description = $"Appointment {appointment.Id} cancelled. Previous status: '{oldStatus}'.",
            Before      = JsonSerializer.Serialize(new { status = oldStatus.ToString() }),
            After       = JsonSerializer.Serialize(new
            {
                status       = AppointmentStatus.Cancelled.ToString(),
                notes        = request.Notes,
                slotReleased = slot is not null,
            }),
            CorrelationId  = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
            RequestId      = _httpContextAccessor.HttpContext?.TraceIdentifier,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "care-connect", "careconnect.appointment.cancelled", appointment.Id.ToString()),
            Tags = ["appointment", "clinical", "cancellation"],
        });

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<AppointmentResponse> RescheduleAppointmentAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        RescheduleAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request.NewAppointmentSlotId == Guid.Empty)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "NewAppointmentSlotId is required." }
                });

        var appointment = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        if (!AppointmentWorkflowRules.IsReschedulable(appointment.Status))
            throw new ConflictException(
                $"Cannot reschedule an appointment with status '{appointment.Status}'.",
                "INVALID_STATE_TRANSITION");

        if (appointment.AppointmentSlotId == request.NewAppointmentSlotId)
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["newAppointmentSlotId"] = new[] { "New slot must be different from the current slot." }
                });

        var newSlot = await _slots.GetByIdAsync(tenantId, request.NewAppointmentSlotId, ct)
            ?? throw new NotFoundException($"Appointment slot '{request.NewAppointmentSlotId}' was not found.");

        if (newSlot.Status != SlotStatus.Open)
            throw new ConflictException(
                "The target slot is not available for booking.",
                "SLOT_CONFLICT");

        if (newSlot.ReservedCount >= newSlot.Capacity)
            throw new ConflictException(
                "The target slot has no remaining capacity.",
                "SLOT_CONFLICT");

        AppointmentSlot? oldSlot = null;
        if (appointment.AppointmentSlotId.HasValue)
        {
            oldSlot = await _slots.GetByIdAsync(tenantId, appointment.AppointmentSlotId.Value, ct);
            oldSlot?.Release(userId);
        }

        newSlot.Reserve(userId);

        var oldStatus = appointment.Status;
        appointment.Reschedule(newSlot, request.Notes, userId);
        appointment.UpdateStatus(AppointmentStatus.Rescheduled, userId);

        var history = AppointmentStatusHistory.Create(
            appointment.Id,
            tenantId,
            oldStatus,
            AppointmentStatus.Rescheduled,
            userId,
            request.Notes);

        await _appointments.SaveRescheduleAsync(appointment, oldSlot, newSlot, history, ct);

        // Notification hook: new AppointmentScheduled + AppointmentReminder for the new slot time.
        try { await _notifications.CreateAppointmentScheduledAsync(tenantId, appointment.Id, newSlot.StartAtUtc, userId, ct); }
        catch { /* Notification failure must not break the reschedule. */ }

        var loaded = await _appointments.GetByIdAsync(tenantId, appointment.Id, ct);
        return ToAppointmentResponse(loaded!);
    }

    public async Task<List<AppointmentStatusHistoryResponse>> GetAppointmentHistoryAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment '{id}' was not found.");

        var rows = await _history.GetByAppointmentIdAsync(tenantId, id, ct);

        return rows.Select(h => new AppointmentStatusHistoryResponse
        {
            Id = h.Id,
            AppointmentId = h.AppointmentId,
            OldStatus = h.OldStatus,
            NewStatus = h.NewStatus,
            ChangedByUserId = h.ChangedByUserId,
            ChangedAtUtc = h.ChangedAtUtc,
            Notes = h.Notes
        }).ToList();
    }

    private static SlotResponse ToSlotResponse(AppointmentSlot s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        ProviderId = s.ProviderId,
        ProviderName = s.Provider?.Name ?? string.Empty,
        FacilityId = s.FacilityId,
        FacilityName = s.Facility?.Name ?? string.Empty,
        ServiceOfferingId = s.ServiceOfferingId,
        ServiceOfferingName = s.ServiceOffering?.Name,
        StartAtUtc = s.StartAtUtc,
        EndAtUtc = s.EndAtUtc,
        Capacity = s.Capacity,
        ReservedCount = s.ReservedCount,
        AvailableCount = s.Capacity - s.ReservedCount,
        Status = s.Status
    };

    private static AppointmentResponse ToAppointmentResponse(Appointment a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        ReferralId = a.ReferralId,
        ProviderId = a.ProviderId,
        ProviderName = a.Provider?.Name ?? string.Empty,
        FacilityId = a.FacilityId,
        FacilityName = a.Facility?.Name ?? string.Empty,
        ServiceOfferingId = a.ServiceOfferingId,
        ServiceOfferingName = a.ServiceOffering?.Name,
        AppointmentSlotId = a.AppointmentSlotId,
        ScheduledStartAtUtc = a.ScheduledStartAtUtc,
        ScheduledEndAtUtc = a.ScheduledEndAtUtc,
        Status = a.Status,
        Notes = a.Notes,
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
        // Phase 5 / LSCC-002: denormalized org participant IDs — populated at creation from Referral
        OrganizationRelationshipId = a.OrganizationRelationshipId,
        ReferringOrganizationId    = a.ReferringOrganizationId,
        ReceivingOrganizationId    = a.ReceivingOrganizationId
    };
}
