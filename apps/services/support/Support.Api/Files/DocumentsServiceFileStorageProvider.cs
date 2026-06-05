using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Support.Api.Configuration;

namespace Support.Api.Files;

/// <summary>
/// LegalSynq integrated mode: forwards the upload to the Documents Service
/// and stores only the returned document reference. If the upstream call
/// fails, the caller MUST treat this as a 502/503 — Support must not create
/// an attachment row for an upload that never landed in Documents.
///
/// Endpoint convention (assumption documented in
/// <c>analysis/SUP-INT-08-report.md</c>):
///   <c>POST {BaseUrl}{UploadPath}</c>
///   multipart/form-data:
///     - file              (the binary)
///     - tenant_id
///     - product           ("support")
///     - related_entity_type  ("support.ticket")
///     - related_entity_id    (the ticket id)
///     - uploaded_by_user_id  (optional)
///   Expected 200/201 JSON response shape: { document_id, file_name, content_type, size }
/// </summary>
public sealed class DocumentsServiceFileStorageProvider : ISupportFileStorageProvider
{
    public const string ProviderName = "documents-service";
    public const string HttpClientName = "support-documents";

    private readonly HttpClient _client;
    private readonly IOptionsMonitor<FileStorageOptions> _options;
    private readonly ILogger<DocumentsServiceFileStorageProvider> _log;

    public DocumentsServiceFileStorageProvider(
        HttpClient client,
        IOptionsMonitor<FileStorageOptions> options,
        ILogger<DocumentsServiceFileStorageProvider> log)
    {
        _client = client;
        _options = options;
        _log = log;
    }

    public async Task<SupportFileUploadResult> UploadAsync(
        SupportFileUploadRequest request,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue.DocumentsService;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            throw new SupportFileStorageNotConfiguredException();
        }

        var uploadPath = string.IsNullOrWhiteSpace(opts.UploadPath)
            ? "/documents/api/documents/upload"
            : opts.UploadPath;
        var url = new Uri(new Uri(opts.BaseUrl), uploadPath);

        using var content = new MultipartFormDataContent();

        // The HttpClient passes through whatever Stream we hand it without
        // buffering the whole payload into memory; HttpClient handles chunked
        // transfer for streamable content.
        var streamContent = new StreamContent(request.Stream);
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);
        }
        content.Add(streamContent, "file", request.FileName);
        content.Add(new StringContent(request.TenantId), "tenant_id");
        content.Add(new StringContent("support"), "product");
        content.Add(new StringContent("support.ticket"), "related_entity_type");
        content.Add(new StringContent(request.TicketId.ToString()), "related_entity_id");
        if (!string.IsNullOrWhiteSpace(request.UploadedByUserId))
        {
            content.Add(new StringContent(request.UploadedByUserId), "uploaded_by_user_id");
        }

        HttpResponseMessage response;
        try
        {
            response = await _client.PostAsync(url, content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Documents Service upload timed out url={Url}", url);
            throw new SupportFileStorageRemoteException(
                "Documents Service upload timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Documents Service upload network failure url={Url}", url);
            throw new SupportFileStorageRemoteException(
                "Documents Service is unreachable.", ex);
        }

        // Always dispose the response so the underlying connection is returned
        // to the pool — failure paths below previously leaked it.
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Documents Service upload non-success status={Status} url={Url}",
                    (int)response.StatusCode, url);
                throw new SupportFileStorageRemoteException(
                    $"Documents Service rejected upload (status {(int)response.StatusCode}).",
                    upstreamStatusCode: (int)response.StatusCode);
            }

            DocumentsServiceUploadResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<DocumentsServiceUploadResponse>(
                    cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                throw new SupportFileStorageRemoteException(
                    "Documents Service returned an unparseable response.", ex,
                    upstreamStatusCode: (int)response.StatusCode);
            }

            if (body is null || string.IsNullOrWhiteSpace(body.DocumentId))
            {
                throw new SupportFileStorageRemoteException(
                    "Documents Service response did not include a document_id.",
                    upstreamStatusCode: (int)response.StatusCode);
            }

            return new SupportFileUploadResult(
                DocumentId: body.DocumentId!,
                FileName: body.FileName ?? request.FileName,
                ContentType: body.ContentType ?? request.ContentType,
                FileSizeBytes: body.Size ?? request.FileSizeBytes,
                StorageProvider: ProviderName,
                StorageUri: body.StorageUri);
        }
    }

    private sealed class DocumentsServiceUploadResponse
    {
        [JsonPropertyName("document_id")] public string? DocumentId { get; set; }
        [JsonPropertyName("file_name")]   public string? FileName { get; set; }
        [JsonPropertyName("content_type")] public string? ContentType { get; set; }
        [JsonPropertyName("size")]        public long? Size { get; set; }
        [JsonPropertyName("storage_uri")] public string? StorageUri { get; set; }
    }
}
