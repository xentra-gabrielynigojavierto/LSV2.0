namespace CareConnect.Application.DTOs;

public class CreateAppointmentRequest
{
    public Guid ReferralId { get; set; }
    public Guid AppointmentSlotId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAppointmentRequest
{
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public class ConfirmAppointmentRequest
{
    public string? Notes { get; set; }
}

public class CompleteAppointmentRequest
{
    public string? Notes { get; set; }
}

public class CancelAppointmentRequest
{
    public string? Notes { get; set; }
}

public class RescheduleAppointmentRequest
{
    public Guid NewAppointmentSlotId { get; set; }
    public string? Notes { get; set; }
}

public class AppointmentStatusHistoryResponse
{
    public Guid Id { get; init; }
    public Guid AppointmentId { get; init; }
    public string OldStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public Guid? ChangedByUserId { get; init; }
    public DateTime ChangedAtUtc { get; init; }
    public string? Notes { get; init; }
}

public class AppointmentResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ReferralId { get; init; }
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public Guid FacilityId { get; init; }
    public string FacilityName { get; init; } = string.Empty;
    public Guid? ServiceOfferingId { get; init; }
    public string? ServiceOfferingName { get; init; }
    public Guid? AppointmentSlotId { get; init; }
    public DateTime ScheduledStartAtUtc { get; init; }
    public DateTime ScheduledEndAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    // Phase 5 / LSCC-002: denormalized from Referral at appointment creation time.
    // Null for appointments created before LSCC-002 org-linkage enforcement.
    public Guid? OrganizationRelationshipId { get; init; }
    public Guid? ReferringOrganizationId { get; init; }
    public Guid? ReceivingOrganizationId { get; init; }
}

public class AppointmentSearchParams
{
    public Guid? ReferralId { get; set; }
    public Guid? ProviderId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
