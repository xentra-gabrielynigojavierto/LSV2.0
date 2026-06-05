using System.Text.Json;
using Comms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Comms.Infrastructure.Documents;

public sealed class DocumentServiceClient : IDocumentServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DocumentServiceClient> _logger;

    public DocumentServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(
        Guid documentId, Guid expectedTenantId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsService");
            var response = await client.GetAsync($"/documents/{documentId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Documents service returned {StatusCode} for document {DocumentId}",
                    response.StatusCode, documentId);
                return new DocumentValidationResult(false, null);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            Guid? docTenantId = null;

            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataElement))
                root = dataElement;

            if (root.TryGetProperty("tenantId", out var tenantProp))
            {
                var tenantStr = tenantProp.ValueKind == JsonValueKind.String
                    ? tenantProp.GetString()
                    : tenantProp.ToString();

                if (Guid.TryParse(tenantStr, out var parsedTenantId))
                    docTenantId = parsedTenantId;
            }

            if (!docTenantId.HasValue)
            {
                _logger.LogWarning(
                    "Document {DocumentId} response missing tenantId — cannot verify tenant ownership",
                    documentId);
                return new DocumentValidationResult(true, null);
            }

            return new DocumentValidationResult(true, docTenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to validate document {DocumentId} with Documents service", documentId);
            return new DocumentValidationResult(false, null);
        }
    }
}
