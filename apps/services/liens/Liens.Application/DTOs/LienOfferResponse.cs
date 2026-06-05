namespace Liens.Application.DTOs;

public sealed class LienOfferResponse
{
    public Guid Id { get; init; }
    public Guid LienId { get; init; }
    public decimal OfferAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid BuyerOrgId { get; init; }
    public Guid SellerOrgId { get; init; }
    public string? Notes { get; init; }
    public string? ResponseNotes { get; init; }
    public string? ExternalReference { get; init; }
    public DateTime OfferedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? RespondedAtUtc { get; init; }
    public DateTime? WithdrawnAtUtc { get; init; }
    public bool IsExpired { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
