namespace CareConnect.Application.Interfaces;

/// <summary>
/// CC2-INT-B03: CareConnect-side client for the platform Documents service.
/// Supports server-side upload proxying (multipart bytes forwarded to Documents)
/// and signed URL retrieval for document access.
///
/// Implementations communicate with the Documents service REST API.
/// Configuration key: DocumentsService:BaseUrl
/// </summary>
public interface IDocumentServiceClient
{
    /// <summary>
    /// Uploads a file to the Documents service and returns the assigned documentId.
    /// CareConnect stores only the documentId locally; no file bytes are persisted.
    /// </summary>
    Task<DocumentUploadResult> UploadAsync(
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
        CancellationToken ct = default);

    /// <summary>
    /// Requests a short-lived signed URL from the Documents service for document access.
    /// Returns the redeem URL string, or null if the document is not accessible.
    /// </summary>
    Task<DocumentSignedUrlResult?> GetSignedUrlAsync(
        string            documentId,
        bool              isDownload = false,
        CancellationToken ct         = default);
}

public record DocumentUploadResult(bool Success, string? DocumentId, string? Error);

public record DocumentSignedUrlResult(string RedeemUrl, int ExpiresInSeconds);
