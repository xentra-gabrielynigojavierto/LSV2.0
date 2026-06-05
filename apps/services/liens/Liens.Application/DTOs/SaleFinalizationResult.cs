namespace Liens.Application.DTOs;

public sealed class SaleFinalizationResult
{
    public Guid AcceptedOfferId { get; init; }
    public string AcceptedOfferStatus { get; init; } = string.Empty;

    public Guid LienId { get; init; }
    public string FinalLienStatus { get; init; } = string.Empty;

    public Guid BillOfSaleId { get; init; }
    public string BillOfSaleNumber { get; init; } = string.Empty;
    public string BillOfSaleStatus { get; init; } = string.Empty;

    public decimal PurchaseAmount { get; init; }
    public decimal OriginalLienAmount { get; init; }
    public decimal? DiscountPercent { get; init; }

    public Guid? DocumentId { get; init; }

    public int CompetingOffersRejected { get; init; }
    public DateTime FinalizedAtUtc { get; init; }
}
