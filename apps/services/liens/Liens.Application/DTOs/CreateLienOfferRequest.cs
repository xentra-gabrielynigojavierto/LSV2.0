namespace Liens.Application.DTOs;

public sealed class CreateLienOfferRequest
{
    public Guid LienId { get; init; }
    public decimal OfferAmount { get; init; }
    public string? Notes { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}
