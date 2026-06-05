using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralAttachmentService
{
    Task<List<AttachmentMetadataResponse>> GetByReferralAsync(Guid tenantId, Guid referralId, Guid? callerOrgId, bool isAdmin, CancellationToken ct = default);

    Task<AttachmentMetadataResponse> CreateAsync(Guid tenantId, Guid referralId, Guid? userId, CreateAttachmentMetadataRequest request, CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B03: Proxies a file upload to the Documents service and persists only
    /// the returned documentId into the local ReferralAttachments table.
    /// </summary>
    Task<AttachmentMetadataResponse> UploadAsync(
        Guid              tenantId,
        Guid              referralId,
        Guid?             userId,
        Stream            fileContent,
        string            fileName,
        string            contentType,
        long              fileSizeBytes,
        UploadAttachmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B03: Enforces scope rules then returns a short-lived signed URL for document access.
    /// Returns null if the caller is not permitted to access the document.
    /// </summary>
    Task<SignedUrlResponse?> GetSignedUrlAsync(
        Guid              tenantId,
        Guid              referralId,
        Guid              attachmentId,
        Guid?             callerOrgId,
        string?           callerOrgType,
        bool              isAdmin,
        bool              isDownload,
        CancellationToken ct = default);
}
