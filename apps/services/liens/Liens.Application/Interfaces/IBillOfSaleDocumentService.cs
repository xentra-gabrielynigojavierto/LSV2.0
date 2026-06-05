using Liens.Application.DTOs;
using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface IBillOfSaleDocumentService
{
    Task<Guid?> GenerateAndStoreAsync(BillOfSale billOfSale, Guid actingUserId, CancellationToken ct = default);

    Task<DocumentRetrievalResult?> RetrieveDocumentAsync(Guid documentId, CancellationToken ct = default);
}
