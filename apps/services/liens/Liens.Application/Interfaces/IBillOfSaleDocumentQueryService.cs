using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IBillOfSaleDocumentQueryService
{
    Task<DocumentRetrievalResult> GetDocumentByBosIdAsync(Guid tenantId, Guid bosId, CancellationToken ct = default);

    Task<DocumentRetrievalResult> GetDocumentByBosNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default);
}
