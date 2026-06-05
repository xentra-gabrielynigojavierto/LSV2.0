namespace Liens.Application.DTOs;

public sealed class CaseResponse
{
    public Guid Id { get; init; }
    public string CaseNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? Title { get; init; }
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? DateOfIncident { get; init; }
    public DateOnly? ClientDob { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddress { get; init; }
    public string? InsuranceCarrier { get; init; }
    public string? PolicyNumber { get; init; }
    public string? ClaimNumber { get; init; }
    public decimal? DemandAmount { get; init; }
    public decimal? SettlementAmount { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public DateTime? OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
