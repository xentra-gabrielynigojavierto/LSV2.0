using System.Net.Http.Headers;
using System.Text.Json;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Documents;

public sealed class BillOfSaleDocumentService : IBillOfSaleDocumentService
{
    private static readonly Guid BillOfSaleDocumentTypeId =
        Guid.Parse("00000000-0000-0000-0000-000000000B05");

    private readonly IBillOfSalePdfGenerator _pdfGenerator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BillOfSaleDocumentService> _logger;

    public BillOfSaleDocumentService(
        IBillOfSalePdfGenerator pdfGenerator,
        IHttpClientFactory httpClientFactory,
        ILogger<BillOfSaleDocumentService> logger)
    {
        _pdfGenerator = pdfGenerator;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Guid?> GenerateAndStoreAsync(
        BillOfSale bos,
        Guid actingUserId,
        CancellationToken ct = default)
    {
        byte[] pdfBytes;
        try
        {
            pdfBytes = _pdfGenerator.Generate(bos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BOS document generation failed: BOS={BosId} Tenant={TenantId}",
                bos.Id, bos.TenantId);
            return null;
        }

        try
        {
            var fileName = $"BOS-{bos.BillOfSaleNumber}.pdf";

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(bos.TenantId.ToString()), "tenantId");
            content.Add(new StringContent(BillOfSaleDocumentTypeId.ToString()), "documentTypeId");
            content.Add(new StringContent("SYNQ_LIENS"), "productId");
            content.Add(new StringContent(bos.Id.ToString()), "referenceId");
            content.Add(new StringContent("BillOfSale"), "referenceType");
            content.Add(new StringContent($"Bill of Sale — {bos.BillOfSaleNumber}"), "title");
            content.Add(new StringContent($"Auto-generated BOS document for {bos.BillOfSaleNumber}"), "description");

            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", fileName);

            var client = _httpClientFactory.CreateClient("DocumentsService");
            var response = await client.PostAsync("/documents", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Documents service returned {StatusCode} for BOS={BosId}: {Body}",
                    response.StatusCode, bos.Id, body);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var idProp) &&
                Guid.TryParse(idProp.GetString(), out var documentId))
            {
                _logger.LogInformation(
                    "BOS document stored: BOS={BosId} DocumentId={DocumentId} Tenant={TenantId}",
                    bos.Id, documentId, bos.TenantId);
                return documentId;
            }

            _logger.LogWarning(
                "Documents service returned unexpected response for BOS={BosId}: {Json}",
                bos.Id, json);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Documents service call failed for BOS={BosId} Tenant={TenantId}",
                bos.Id, bos.TenantId);
            return null;
        }
    }

    public async Task<DocumentRetrievalResult?> RetrieveDocumentAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        HttpResponseMessage? response = null;
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsService");

            response = await client.GetAsync(
                $"/documents/{documentId}/content?type=download",
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Documents service returned 404 for DocumentId={DocumentId}",
                    documentId);
                response.Dispose();
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Documents service returned {StatusCode} for DocumentId={DocumentId}",
                    response.StatusCode, documentId);
                response.Dispose();
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                           ?? $"document-{documentId}.pdf";

            var contentLength = response.Content.Headers.ContentLength;

            var stream = await response.Content.ReadAsStreamAsync(ct);

            return new DocumentRetrievalResult
            {
                Content = stream,
                ContentType = contentType,
                FileName = fileName,
                ContentLength = contentLength,
                ResponseOwner = response,
            };
        }
        catch (OperationCanceledException)
        {
            response?.Dispose();
            throw;
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            _logger.LogError(ex,
                "Documents service request failed for DocumentId={DocumentId}",
                documentId);
            return null;
        }
        catch (Exception ex)
        {
            response?.Dispose();
            _logger.LogError(ex,
                "Unexpected error retrieving document DocumentId={DocumentId}",
                documentId);
            return null;
        }
    }
}
