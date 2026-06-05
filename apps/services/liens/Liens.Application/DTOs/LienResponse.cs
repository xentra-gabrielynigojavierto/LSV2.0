namespace Liens.Application.DTOs;

public sealed class LienResponse
{
    public Guid Id { get; init; }
    public string LienNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string LienType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? CaseId { get; init; }
    public Guid? FacilityId { get; init; }
    public decimal OriginalAmount { get; init; }
    public decimal? CurrentBalance { get; init; }
    public decimal? OfferPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? PayoffAmount { get; init; }
    public string? Jurisdiction { get; init; }
    public bool IsConfidential { get; init; }
    public string? SubjectFirstName { get; init; }
    public string? SubjectLastName { get; init; }
    public string? SubjectDisplayName { get; init; }
    public Guid OrgId { get; init; }
    public Guid? SellingOrgId { get; init; }
    public Guid? BuyingOrgId { get; init; }
    public Guid? HoldingOrgId { get; init; }
    public DateOnly? IncidentDate { get; init; }
    public string? Description { get; init; }
    public DateTime? OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
