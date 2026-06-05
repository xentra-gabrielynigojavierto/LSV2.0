namespace Liens.Application.DTOs;

public sealed class BillOfSaleResponse
{
    public Guid Id { get; init; }
    public string BillOfSaleNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid LienId { get; init; }
    public Guid LienOfferId { get; init; }
    public Guid SellerOrgId { get; init; }
    public Guid BuyerOrgId { get; init; }
    public decimal PurchaseAmount { get; init; }
    public decimal OriginalLienAmount { get; init; }
    public decimal? DiscountPercent { get; init; }
    public string? SellerContactName { get; init; }
    public string? BuyerContactName { get; init; }
    public string? Terms { get; init; }
    public string? Notes { get; init; }
    public Guid? DocumentId { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public DateTime? ExecutedAtUtc { get; init; }
    public DateTime? EffectiveAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
