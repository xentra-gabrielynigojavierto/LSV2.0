using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Case : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid OrgId            { get; private set; }

    public string CaseNumber     { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }
    public string? Title         { get; private set; }

    public string ClientFirstName { get; private set; } = string.Empty;
    public string ClientLastName  { get; private set; } = string.Empty;
    public DateOnly? ClientDob    { get; private set; }
    public string? ClientPhone    { get; private set; }
    public string? ClientEmail    { get; private set; }
    public string? ClientAddress  { get; private set; }

    public string Status          { get; private set; } = CaseStatus.PreDemand;
    public DateOnly? DateOfIncident { get; private set; }
    public DateTime? OpenedAtUtc   { get; private set; }
    public DateTime? ClosedAtUtc   { get; private set; }

    public string? InsuranceCarrier { get; private set; }
    public string? PolicyNumber     { get; private set; }
    public string? ClaimNumber      { get; private set; }

    public decimal? DemandAmount     { get; private set; }
    public decimal? SettlementAmount { get; private set; }

    public string? Description { get; private set; }
    public string? Notes       { get; private set; }

    private Case() { }

    public static Case Create(
        Guid tenantId,
        Guid orgId,
        string caseNumber,
        string clientFirstName,
        string clientLastName,
        Guid createdByUserId,
        string? externalReference = null,
        string? title = null,
        DateOnly? clientDob = null,
        string? clientPhone = null,
        string? clientEmail = null,
        string? clientAddress = null,
        DateOnly? dateOfIncident = null,
        string? insuranceCarrier = null,
        string? policyNumber = null,
        string? claimNumber = null,
        string? description = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientLastName);

        var now = DateTime.UtcNow;
        return new Case
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            OrgId             = orgId,
            CaseNumber        = caseNumber.Trim(),
            ExternalReference = externalReference?.Trim(),
            Title             = title?.Trim(),
            ClientFirstName   = clientFirstName.Trim(),
            ClientLastName    = clientLastName.Trim(),
            ClientDob         = clientDob,
            ClientPhone       = clientPhone?.Trim(),
            ClientEmail       = clientEmail?.Trim(),
            ClientAddress     = clientAddress?.Trim(),
            Status            = CaseStatus.PreDemand,
            DateOfIncident    = dateOfIncident,
            OpenedAtUtc       = now,
            InsuranceCarrier  = insuranceCarrier?.Trim(),
            PolicyNumber      = policyNumber?.Trim(),
            ClaimNumber       = claimNumber?.Trim(),
            Description       = description?.Trim(),
            Notes             = notes?.Trim(),
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string clientFirstName,
        string clientLastName,
        Guid updatedByUserId,
        string? title = null,
        string? externalReference = null,
        DateOnly? clientDob = null,
        string? clientPhone = null,
        string? clientEmail = null,
        string? clientAddress = null,
        DateOnly? dateOfIncident = null,
        string? insuranceCarrier = null,
        string? policyNumber = null,
        string? claimNumber = null,
        string? description = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientLastName);

        ClientFirstName   = clientFirstName.Trim();
        ClientLastName    = clientLastName.Trim();
        Title             = title?.Trim();
        ExternalReference = externalReference?.Trim();
        ClientDob         = clientDob;
        ClientPhone       = clientPhone?.Trim();
        ClientEmail       = clientEmail?.Trim();
        ClientAddress     = clientAddress?.Trim();
        DateOfIncident    = dateOfIncident;
        InsuranceCarrier  = insuranceCarrier?.Trim();
        PolicyNumber      = policyNumber?.Trim();
        ClaimNumber       = claimNumber?.Trim();
        Description       = description?.Trim();
        Notes             = notes?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!CaseStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid case status: '{newStatus}'.");

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (newStatus == CaseStatus.Closed)
            ClosedAtUtc = DateTime.UtcNow;
    }

    public void SetDemandAmount(decimal amount, Guid updatedByUserId)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Demand amount cannot be negative.");

        DemandAmount    = amount;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SetSettlementAmount(decimal amount, Guid updatedByUserId)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Settlement amount cannot be negative.");

        SettlementAmount = amount;
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
    }
}
