using System.Net.Http.Headers;
using System.Text.Json;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Documents;

/// <summary>
/// CC2-INT-B03: HTTP client for the platform Documents service.
/// Handles file upload proxying and signed URL retrieval.
///
/// Configuration keys:
///   DocumentsService:BaseUrl        — default http://localhost:5006
///   DocumentsService:ServiceToken   — Bearer token for service-to-service auth
///   DocumentsService:ProductId      — productId field required by Documents API (default "CareConnect")
///   DocumentsService:DocumentTypeId — documentTypeId UUID required by Documents API
/// </summary>
public sealed class DocumentServiceClient : IDocumentServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string             _baseUrl;
    private readonly string?            _serviceToken;
    private readonly string             _defaultProductId;
    private readonly string?            _defaultDocumentTypeId;
    private readonly ILogger<DocumentServiceClient> _logger;

    public DocumentServiceClient(
        IHttpClientFactory                    httpClientFactory,
        IConfiguration                        configuration,
        ILogger<DocumentServiceClient>        logger)
    {
        _httpClientFactory     = httpClientFactory;
        _baseUrl               = (configuration["DocumentsService:BaseUrl"] ?? "http://localhost:5006").TrimEnd('/');
        _serviceToken          = configuration["DocumentsService:ServiceToken"];
        _defaultProductId      = configuration["DocumentsService:ProductId"] ?? "CareConnect";
        _defaultDocumentTypeId = configuration["DocumentsService:DocumentTypeId"];
        _logger                = logger;
    }

    public async Task<DocumentUploadResult> UploadAsync(
        Stream      fileContent,
        string      fileName,
        string      contentType,
        long        fileSizeBytes,
        Guid        tenantId,
        string      title,
        string?     productId      = null,
        string?     documentTypeId = null,
        string?     referenceId    = null,
        string?     referenceType  = null,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("DocumentsService");
        var url    = $"{_baseUrl}/documents";

        if (!string.IsNullOrWhiteSpace(_serviceToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _serviceToken);

        using var form = new MultipartFormDataContent();

        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileStreamContent, "file", fileName);
        form.Add(new StringContent(tenantId.ToString()), "tenantId");
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(productId ?? _defaultProductId), "productId");

        var effectiveDocTypeId = documentTypeId ?? _defaultDocumentTypeId;
        if (!string.IsNullOrWhiteSpace(effectiveDocTypeId))
            form.Add(new StringContent(effectiveDocTypeId), "documentTypeId");

        if (!string.IsNullOrWhiteSpace(referenceId))
            form.Add(new StringContent(referenceId), "referenceId");
        if (!string.IsNullOrWhiteSpace(referenceType))
            form.Add(new StringContent(referenceType), "referenceType");

        _logger.LogInformation(
            "Uploading document to Documents service: fileName={FileName} contentType={ContentType} size={Size} tenant={TenantId}",
            fileName, contentType, fileSizeBytes, tenantId);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(url, form, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Documents service unreachable during upload: fileName={FileName} tenant={TenantId}", fileName, tenantId);
            return new DocumentUploadResult(false, null, "Documents service is unreachable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(response, ct);
            _logger.LogWarning(
                "Documents service returned {StatusCode} for upload: fileName={FileName} tenant={TenantId} body={Body}",
                (int)response.StatusCode, fileName, tenantId, body);
            return new DocumentUploadResult(false, null, $"Documents service returned {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var dataEl)) root = dataEl;

        if (!root.TryGetProperty("id", out var idProp))
        {
            _logger.LogWarning(
                "Documents service upload response missing 'id' field: fileName={FileName} tenant={TenantId}",
                fileName, tenantId);
            return new DocumentUploadResult(false, null, "Documents service response missing document id.");
        }

        var documentId = idProp.GetString();
        _logger.LogInformation(
            "Document uploaded successfully: documentId={DocumentId} fileName={FileName} tenant={TenantId}",
            documentId, fileName, tenantId);
        return new DocumentUploadResult(true, documentId, null);
    }

    public async Task<DocumentSignedUrlResult?> GetSignedUrlAsync(
        string            documentId,
        bool              isDownload = false,
        CancellationToken ct         = default)
    {
        var client = _httpClientFactory.CreateClient("DocumentsService");

        if (!string.IsNullOrWhiteSpace(_serviceToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _serviceToken);

        var path   = isDownload
            ? $"{_baseUrl}/documents/{documentId}/download-url"
            : $"{_baseUrl}/documents/{documentId}/view-url";

        _logger.LogInformation(
            "Requesting signed URL from Documents service: documentId={DocumentId} isDownload={IsDownload}",
            documentId, isDownload);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(path, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Documents service unreachable during signed URL request for documentId={DocumentId}", documentId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(response, ct);
            _logger.LogWarning(
                "Documents service returned {StatusCode} for signed URL: documentId={DocumentId} body={Body}",
                (int)response.StatusCode, documentId, body);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var dataEl)) root = dataEl;

        if (!root.TryGetProperty("redeemUrl", out var redeemProp))
        {
            _logger.LogWarning(
                "Documents service signed URL response missing 'redeemUrl': documentId={DocumentId}", documentId);
            return null;
        }

        var redeemUrl      = redeemProp.GetString() ?? string.Empty;
        var expiresSeconds = root.TryGetProperty("expiresInSeconds", out var expProp)
            ? expProp.GetInt32() : 300;

        if (!redeemUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            redeemUrl = $"{_baseUrl}{redeemUrl}";

        return new DocumentSignedUrlResult(redeemUrl, expiresSeconds);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }
}
