using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ProviderAvailabilityException : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid? FacilityId { get; private set; }
    public DateTime StartAtUtc { get; private set; }
    public DateTime EndAtUtc { get; private set; }
    public string ExceptionType { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public bool IsActive { get; private set; }

    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }

    private ProviderAvailabilityException() { }

    public static ProviderAvailabilityException Create(
        Guid tenantId,
        Guid providerId,
        Guid? facilityId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string exceptionType,
        string? reason,
        bool isActive,
        Guid? createdByUserId)
    {
        return new ProviderAvailabilityException
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            ProviderId    = providerId,
            FacilityId    = facilityId,
            StartAtUtc    = startAtUtc,
            EndAtUtc      = endAtUtc,
            ExceptionType = exceptionType,
            Reason        = reason?.Trim(),
            IsActive      = isActive,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc  = DateTime.UtcNow,
            UpdatedAtUtc  = DateTime.UtcNow
        };
    }

    public void Update(
        Guid? facilityId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string exceptionType,
        string? reason,
        bool isActive,
        Guid? updatedByUserId)
    {
        FacilityId    = facilityId;
        StartAtUtc    = startAtUtc;
        EndAtUtc      = endAtUtc;
        ExceptionType = exceptionType;
        Reason        = reason?.Trim();
        IsActive      = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc  = DateTime.UtcNow;
    }

    public bool OverlapsWith(DateTime slotStart, DateTime slotEnd)
        => slotStart < EndAtUtc && slotEnd > StartAtUtc;
}
