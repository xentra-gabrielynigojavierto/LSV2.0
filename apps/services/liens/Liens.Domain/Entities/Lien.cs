using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Lien : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid OrgId            { get; private set; }

    public string LienNumber     { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }

    public string LienType       { get; private set; } = Enums.LienType.MedicalLien;
    public string Status         { get; private set; } = LienStatus.Draft;

    public Guid? CaseId          { get; private set; }
    public Guid? FacilityId      { get; private set; }
    public Guid? SubjectPartyId  { get; private set; }

    public string? SubjectFirstName { get; private set; }
    public string? SubjectLastName  { get; private set; }
    public bool IsConfidential      { get; private set; }

    public decimal OriginalAmount   { get; private set; }
    public decimal? CurrentBalance  { get; private set; }
    public decimal? OfferPrice      { get; private set; }
    public decimal? PurchasePrice   { get; private set; }
    public decimal? PayoffAmount    { get; private set; }

    public string? Jurisdiction  { get; private set; }
    public string? Description   { get; private set; }
    public string? Notes         { get; private set; }

    public DateOnly? IncidentDate { get; private set; }
    public DateTime? OpenedAtUtc  { get; private set; }
    public DateTime? ClosedAtUtc  { get; private set; }

    public Guid? SellingOrgId  { get; private set; }
    public Guid? BuyingOrgId   { get; private set; }
    public Guid? HoldingOrgId  { get; private set; }

    private Lien() { }

    public static Lien Create(
        Guid tenantId,
        Guid orgId,
        string lienNumber,
        string lienType,
        decimal originalAmount,
        Guid createdByUserId,
        string? externalReference = null,
        Guid? caseId = null,
        Guid? facilityId = null,
        Guid? subjectPartyId = null,
        string? subjectFirstName = null,
        string? subjectLastName = null,
        bool isConfidential = false,
        string? jurisdiction = null,
        DateOnly? incidentDate = null,
        string? description = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(lienNumber);

        if (!Enums.LienType.All.Contains(lienType))
            throw new ArgumentException($"Invalid lien type: '{lienType}'.");

        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");

        var now = DateTime.UtcNow;
        return new Lien
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            OrgId             = orgId,
            LienNumber        = lienNumber.Trim(),
            ExternalReference = externalReference?.Trim(),
            LienType          = lienType,
            Status            = LienStatus.Draft,
            CaseId            = caseId,
            FacilityId        = facilityId,
            SubjectPartyId    = subjectPartyId,
            SubjectFirstName  = subjectFirstName?.Trim(),
            SubjectLastName   = subjectLastName?.Trim(),
            IsConfidential    = isConfidential,
            OriginalAmount    = originalAmount,
            CurrentBalance    = originalAmount,
            Jurisdiction      = jurisdiction?.Trim(),
            IncidentDate      = incidentDate,
            Description       = description?.Trim(),
            Notes             = notes?.Trim(),
            OpenedAtUtc       = now,
            SellingOrgId      = orgId,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string lienType,
        decimal originalAmount,
        Guid updatedByUserId,
        string? externalReference = null,
        string? subjectFirstName = null,
        string? subjectLastName = null,
        bool? isConfidential = null,
        string? jurisdiction = null,
        DateOnly? incidentDate = null,
        string? description = null,
        string? notes = null)
    {
        if (!Enums.LienType.All.Contains(lienType))
            throw new ArgumentException($"Invalid lien type: '{lienType}'.");

        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");

        if (!LienStatus.Open.Contains(Status))
            throw new InvalidOperationException($"Cannot update a lien in terminal status '{Status}'.");

        LienType          = lienType;
        OriginalAmount    = originalAmount;
        ExternalReference = externalReference?.Trim();
        SubjectFirstName  = subjectFirstName?.Trim();
        SubjectLastName   = subjectLastName?.Trim();
        if (isConfidential.HasValue) IsConfidential = isConfidential.Value;
        Jurisdiction      = jurisdiction?.Trim();
        IncidentDate      = incidentDate;
        Description       = description?.Trim();
        Notes             = notes?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!LienStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid lien status: '{newStatus}'.");

        if (!LienStatus.AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{newStatus}'.");

        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (LienStatus.Terminal.Contains(newStatus))
            ClosedAtUtc = DateTime.UtcNow;
    }

    public void ListForSale(decimal offerPrice, Guid updatedByUserId, string? offerNotes = null)
    {
        if (Status != LienStatus.Draft)
            throw new InvalidOperationException($"Only draft liens can be listed for sale. Current status: '{Status}'.");

        if (offerPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(offerPrice), "Offer price must be positive.");

        OfferPrice      = offerPrice;
        Status          = LienStatus.Offered;
        Notes           = offerNotes?.Trim() ?? Notes;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Withdraw(Guid updatedByUserId)
    {
        if (Status != LienStatus.Offered && Status != LienStatus.UnderReview)
            throw new InvalidOperationException($"Only offered or under-review liens can be withdrawn. Current status: '{Status}'.");

        Status          = LienStatus.Withdrawn;
        ClosedAtUtc     = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void MarkSold(decimal purchasePrice, Guid buyingOrgId, Guid updatedByUserId)
    {
        if (Status != LienStatus.Offered && Status != LienStatus.UnderReview)
            throw new InvalidOperationException($"Only offered or under-review liens can be sold. Current status: '{Status}'.");

        if (purchasePrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Purchase price must be positive.");

        if (buyingOrgId == Guid.Empty)
            throw new ArgumentException("BuyingOrgId is required.", nameof(buyingOrgId));

        PurchasePrice   = purchasePrice;
        BuyingOrgId     = buyingOrgId;
        HoldingOrgId    = buyingOrgId;
        Status          = LienStatus.Sold;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Activate(Guid updatedByUserId)
    {
        if (Status != LienStatus.Sold)
            throw new InvalidOperationException($"Only sold liens can be activated. Current status: '{Status}'.");

        Status          = LienStatus.Active;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Settle(decimal payoffAmount, Guid updatedByUserId)
    {
        if (Status != LienStatus.Active && Status != LienStatus.Disputed)
            throw new InvalidOperationException($"Only active or disputed liens can be settled. Current status: '{Status}'.");

        if (payoffAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(payoffAmount), "Payoff amount cannot be negative.");

        PayoffAmount    = payoffAmount;
        CurrentBalance  = 0;
        Status          = LienStatus.Settled;
        ClosedAtUtc     = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SetFinancials(
        decimal originalAmount,
        Guid updatedByUserId,
        decimal? currentBalance = null,
        decimal? offerPrice = null,
        decimal? purchasePrice = null,
        decimal? payoffAmount = null)
    {
        if (originalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount cannot be negative.");
        if (currentBalance.HasValue && currentBalance.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(currentBalance), "Current balance cannot be negative.");
        if (offerPrice.HasValue && offerPrice.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(offerPrice), "Offer price cannot be negative.");
        if (purchasePrice.HasValue && purchasePrice.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Purchase price cannot be negative.");
        if (payoffAmount.HasValue && payoffAmount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(payoffAmount), "Payoff amount cannot be negative.");

        OriginalAmount = originalAmount;
        if (currentBalance.HasValue) CurrentBalance = currentBalance.Value;
        if (offerPrice.HasValue)     OfferPrice     = offerPrice.Value;
        if (purchasePrice.HasValue)  PurchasePrice  = purchasePrice.Value;
        if (payoffAmount.HasValue)   PayoffAmount   = payoffAmount.Value;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void AttachCase(Guid caseId, Guid updatedByUserId)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId is required.", nameof(caseId));

        CaseId          = caseId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void AttachFacility(Guid facilityId, Guid updatedByUserId)
    {
        if (facilityId == Guid.Empty) throw new ArgumentException("FacilityId is required.", nameof(facilityId));

        FacilityId      = facilityId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void TransferHolding(Guid newHoldingOrgId, Guid updatedByUserId)
    {
        if (newHoldingOrgId == Guid.Empty) throw new ArgumentException("NewHoldingOrgId is required.", nameof(newHoldingOrgId));

        HoldingOrgId    = newHoldingOrgId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
