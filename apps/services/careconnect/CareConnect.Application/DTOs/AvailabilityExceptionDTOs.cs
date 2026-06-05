namespace CareConnect.Application.DTOs;

public class CreateAvailabilityExceptionRequest
{
    public Guid? FacilityId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateAvailabilityExceptionRequest
{
    public Guid? FacilityId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
}

public class AvailabilityExceptionResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ProviderId { get; init; }
    public Guid? FacilityId { get; init; }
    public string? FacilityName { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string ExceptionType { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public class ApplyExceptionsResponse
{
    public Guid ProviderId { get; init; }
    public int SlotsBlocked { get; init; }
    public int SlotsSkipped { get; init; }
}
