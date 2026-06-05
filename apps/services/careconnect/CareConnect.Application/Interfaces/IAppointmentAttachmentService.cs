using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAppointmentAttachmentService
{
    Task<List<AttachmentMetadataResponse>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, Guid? callerOrgId, bool isAdmin, CancellationToken ct = default);

    Task<AttachmentMetadataResponse> CreateAsync(Guid tenantId, Guid appointmentId, Guid? userId, CreateAttachmentMetadataRequest request, CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B03: Proxies a file upload to the Documents service and persists only
    /// the returned documentId into the local AppointmentAttachments table.
    /// </summary>
    Task<AttachmentMetadataResponse> UploadAsync(
        Guid              tenantId,
        Guid              appointmentId,
        Guid?             userId,
        Stream            fileContent,
        string            fileName,
        string            contentType,
        long              fileSizeBytes,
        UploadAttachmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B03: Returns a short-lived signed URL for appointment document access.
    /// Returns null if the caller is not permitted or document is not accessible.
    /// </summary>
    Task<SignedUrlResponse?> GetSignedUrlAsync(
        Guid              tenantId,
        Guid              appointmentId,
        Guid              attachmentId,
        Guid?             callerOrgId,
        string?           callerOrgType,
        bool              isAdmin,
        bool              isDownload,
        CancellationToken ct = default);
}
