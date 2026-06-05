namespace Liens.Application.DTOs;

public sealed class UpdateCaseRequest
{
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? Title { get; init; }
    public DateOnly? ClientDob { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddress { get; init; }
    public DateOnly? DateOfIncident { get; init; }
    public string? InsuranceCarrier { get; init; }
    public string? PolicyNumber { get; init; }
    public string? ClaimNumber { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Status { get; init; }
    public decimal? DemandAmount { get; init; }
    public decimal? SettlementAmount { get; init; }
}
