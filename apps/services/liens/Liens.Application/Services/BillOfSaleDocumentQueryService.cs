using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class BillOfSaleDocumentQueryService : IBillOfSaleDocumentQueryService
{
    private readonly IBillOfSaleRepository _bosRepo;
    private readonly IBillOfSaleDocumentService _docService;
    private readonly ILogger<BillOfSaleDocumentQueryService> _logger;

    public BillOfSaleDocumentQueryService(
        IBillOfSaleRepository bosRepo,
        IBillOfSaleDocumentService docService,
        ILogger<BillOfSaleDocumentQueryService> logger)
    {
        _bosRepo = bosRepo;
        _docService = docService;
        _logger = logger;
    }

    public async Task<DocumentRetrievalResult> GetDocumentByBosIdAsync(
        Guid tenantId, Guid bosId, CancellationToken ct = default)
    {
        var bos = await _bosRepo.GetByIdAsync(tenantId, bosId, ct)
            ?? throw new NotFoundException($"BillOfSale '{bosId}' not found for tenant '{tenantId}'.");

        return await RetrieveDocumentForBos(bos.Id, bos.DocumentId, bos.BillOfSaleNumber, ct);
    }

    public async Task<DocumentRetrievalResult> GetDocumentByBosNumberAsync(
        Guid tenantId, string billOfSaleNumber, CancellationToken ct = default)
    {
        var bos = await _bosRepo.GetByBillOfSaleNumberAsync(tenantId, billOfSaleNumber, ct)
            ?? throw new NotFoundException($"BillOfSale with number '{billOfSaleNumber}' not found for tenant '{tenantId}'.");

        return await RetrieveDocumentForBos(bos.Id, bos.DocumentId, bos.BillOfSaleNumber, ct);
    }

    private async Task<DocumentRetrievalResult> RetrieveDocumentForBos(
        Guid bosId, Guid? documentId, string bosNumber, CancellationToken ct)
    {
        if (!documentId.HasValue)
            throw new ConflictException(
                $"BillOfSale '{bosId}' does not have a document attached. " +
                "The document may not have been generated yet or generation failed.",
                "DOCUMENT_NOT_AVAILABLE");

        var result = await _docService.RetrieveDocumentAsync(documentId.Value, ct);
        if (result is null)
        {
            _logger.LogError(
                "Documents service failed to return document for BOS={BosId} DocumentId={DocumentId}",
                bosId, documentId.Value);
            throw new ServiceUnavailableException(
                $"Unable to retrieve document for BillOfSale '{bosId}'. The document storage service may be temporarily unavailable.");
        }

        return result;
    }
}
