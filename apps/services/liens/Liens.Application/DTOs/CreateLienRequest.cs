namespace Liens.Application.DTOs;

public sealed class CreateLienRequest
{
    public string LienNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string LienType { get; init; } = string.Empty;
    public Guid? CaseId { get; init; }
    public Guid? FacilityId { get; init; }
    public decimal OriginalAmount { get; init; }
    public string? Jurisdiction { get; init; }
    public bool IsConfidential { get; init; }
    public string? SubjectFirstName { get; init; }
    public string? SubjectLastName { get; init; }
    public DateOnly? IncidentDate { get; init; }
    public string? Description { get; init; }
}
