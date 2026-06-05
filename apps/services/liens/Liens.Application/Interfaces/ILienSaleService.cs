using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienSaleService
{
    Task<SaleFinalizationResult> AcceptOfferAsync(
        Guid tenantId,
        Guid lienOfferId,
        Guid actingUserId,
        CancellationToken ct = default);
}
