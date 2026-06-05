using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class BillOfSale : AuditableEntity
{
    public Guid Id               { get; private set; }
    public Guid TenantId         { get; private set; }
    public Guid LienId           { get; private set; }
    public Guid LienOfferId      { get; private set; }

    public string BillOfSaleNumber { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }

    public string Status { get; private set; } = BillOfSaleStatus.Draft;

    public Guid SellerOrgId      { get; private set; }
    public Guid BuyerOrgId       { get; private set; }

    public decimal PurchaseAmount  { get; private set; }
    public decimal OriginalLienAmount { get; private set; }
    public decimal? DiscountPercent { get; private set; }

    public string? SellerContactName { get; private set; }
    public string? BuyerContactName  { get; private set; }

    public string? Terms { get; private set; }
    public string? Notes { get; private set; }

    public Guid? DocumentId { get; private set; }

    public DateTime IssuedAtUtc      { get; private set; }
    public DateTime? ExecutedAtUtc   { get; private set; }
    public DateTime? EffectiveAtUtc  { get; private set; }
    public DateTime? CancelledAtUtc  { get; private set; }

    private BillOfSale() { }

    public static BillOfSale CreateFromAcceptedOffer(
        Guid tenantId,
        Guid lienId,
        Guid lienOfferId,
        string billOfSaleNumber,
        Guid sellerOrgId,
        Guid buyerOrgId,
        decimal purchaseAmount,
        decimal originalLienAmount,
        Guid createdByUserId,
        string? externalReference = null,
        string? sellerContactName = null,
        string? buyerContactName = null,
        string? terms = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (lienId == Guid.Empty) throw new ArgumentException("LienId is required.", nameof(lienId));
        if (lienOfferId == Guid.Empty) throw new ArgumentException("LienOfferId is required.", nameof(lienOfferId));
        if (sellerOrgId == Guid.Empty) throw new ArgumentException("SellerOrgId is required.", nameof(sellerOrgId));
        if (buyerOrgId == Guid.Empty) throw new ArgumentException("BuyerOrgId is required.", nameof(buyerOrgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(billOfSaleNumber);

        if (purchaseAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(purchaseAmount), "Purchase amount must be positive.");

        if (originalLienAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(originalLienAmount), "Original lien amount cannot be negative.");

        decimal? discount = originalLienAmount > 0
            ? Math.Round((1m - purchaseAmount / originalLienAmount) * 100m, 2)
            : null;

        var now = DateTime.UtcNow;
        return new BillOfSale
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            LienId             = lienId,
            LienOfferId        = lienOfferId,
            BillOfSaleNumber   = billOfSaleNumber.Trim(),
            ExternalReference  = externalReference?.Trim(),
            Status             = BillOfSaleStatus.Draft,
            SellerOrgId        = sellerOrgId,
            BuyerOrgId         = buyerOrgId,
            PurchaseAmount     = purchaseAmount,
            OriginalLienAmount = originalLienAmount,
            DiscountPercent    = discount,
            SellerContactName  = sellerContactName?.Trim(),
            BuyerContactName   = buyerContactName?.Trim(),
            Terms              = terms?.Trim(),
            Notes              = notes?.Trim(),
            IssuedAtUtc        = now,
            CreatedByUserId    = createdByUserId,
            UpdatedByUserId    = createdByUserId,
            CreatedAtUtc       = now,
            UpdatedAtUtc       = now,
        };
    }

    public void SubmitForExecution(Guid updatedByUserId)
    {
        EnsureStatus(BillOfSaleStatus.Draft, "submit for execution");

        Status          = BillOfSaleStatus.Pending;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void MarkExecuted(Guid updatedByUserId, DateTime? effectiveAtUtc = null)
    {
        EnsureStatus(BillOfSaleStatus.Pending, "execute");

        var now = DateTime.UtcNow;
        Status          = BillOfSaleStatus.Executed;
        ExecutedAtUtc   = now;
        EffectiveAtUtc  = effectiveAtUtc ?? now;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = now;
    }

    public void Cancel(Guid updatedByUserId, string? cancellationReason = null)
    {
        if (BillOfSaleStatus.Terminal.Contains(Status))
            throw new InvalidOperationException(
                $"Cannot cancel a bill of sale in terminal status '{Status}'.");

        var now = DateTime.UtcNow;
        Status          = BillOfSaleStatus.Cancelled;
        CancelledAtUtc  = now;
        if (cancellationReason != null)
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"[Cancelled] {cancellationReason.Trim()}"
                : $"{Notes}\n[Cancelled] {cancellationReason.Trim()}";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = now;
    }

    public void UpdateDraft(
        Guid updatedByUserId,
        string? externalReference = null,
        string? sellerContactName = null,
        string? buyerContactName = null,
        string? terms = null,
        string? notes = null)
    {
        EnsureStatus(BillOfSaleStatus.Draft, "update");

        ExternalReference = externalReference?.Trim();
        SellerContactName = sellerContactName?.Trim();
        BuyerContactName  = buyerContactName?.Trim();
        Terms             = terms?.Trim();
        Notes             = notes?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void AttachDocument(Guid documentId, Guid updatedByUserId)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId is required.", nameof(documentId));

        DocumentId      = documentId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public bool IsTerminal => BillOfSaleStatus.Terminal.Contains(Status);

    private void EnsureStatus(string requiredStatus, string action)
    {
        if (Status != requiredStatus)
            throw new InvalidOperationException(
                $"Cannot {action} a bill of sale in status '{Status}'. Required: '{requiredStatus}'.");
    }
}
