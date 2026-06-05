using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AppointmentAttachmentService : IAppointmentAttachmentService
{
    private readonly IAppointmentAttachmentRepository _attachments;
    private readonly IAppointmentRepository           _appointments;
    private readonly IDocumentServiceClient           _documents;

    public AppointmentAttachmentService(
        IAppointmentAttachmentRepository attachments,
        IAppointmentRepository           appointments,
        IDocumentServiceClient           documents)
    {
        _attachments  = attachments;
        _appointments = appointments;
        _documents    = documents;
    }

    public async Task<List<AttachmentMetadataResponse>> GetByAppointmentAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? callerOrgId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        if (!isAdmin)
        {
            var isParticipant =
                (callerOrgId.HasValue && appointment.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && appointment.ReceivingOrganizationId  == callerOrgId);

            if (!isParticipant)
                throw new NotFoundException($"Appointment '{appointmentId}' was not found.");
        }

        var rows = await _attachments.GetByAppointmentAsync(tenantId, appointmentId, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AttachmentMetadataResponse> CreateAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? userId,
        CreateAttachmentMetadataRequest request,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        ValidateAttachmentRequest(request);

        var attachment = AppointmentAttachment.Create(
            tenantId,
            appointmentId,
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            request.ExternalDocumentId,
            request.ExternalStorageProvider,
            request.Status,
            request.Notes,
            userId);

        await _attachments.AddAsync(attachment, ct);
        return ToResponse(attachment);
    }

    /// <summary>
    /// CC2-INT-B03: Proxies the file bytes to the Documents service; stores only the returned
    /// documentId locally. File bytes are never persisted by CareConnect.
    /// </summary>
    public async Task<AttachmentMetadataResponse> UploadAsync(
        Guid              tenantId,
        Guid              appointmentId,
        Guid?             userId,
        Stream            fileContent,
        string            fileName,
        string            contentType,
        long              fileSizeBytes,
        UploadAttachmentRequest request,
        CancellationToken ct = default)
    {
        _ = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        var uploadResult = await _documents.UploadAsync(
            fileContent,
            fileName,
            contentType,
            fileSizeBytes,
            tenantId,
            title:         fileName,
            referenceId:   appointmentId.ToString(),
            referenceType: "appointment",
            ct:            ct);

        if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.DocumentId))
            throw new InvalidOperationException(
                $"Document upload failed: {uploadResult.Error ?? "unknown error"}");

        // CC2-INT-B03: ExternalStorageProvider stores the access scope ("shared" or "provider-specific").
        // EnforceScope reads this field to decide who may request a signed URL for this attachment.
        var attachment = AppointmentAttachment.Create(
            tenantId,
            appointmentId,
            fileName,
            contentType,
            fileSizeBytes,
            externalDocumentId:      uploadResult.DocumentId,
            externalStorageProvider: request.Scope,
            status:                  "Uploaded",
            notes:                   request.Notes,
            createdByUserId:         userId);

        await _attachments.AddAsync(attachment, ct);
        return ToResponse(attachment);
    }

    /// <summary>
    /// CC2-INT-B03: Returns a short-lived signed URL for appointment document access.
    /// Caller must be a participant in the associated appointment or an admin.
    /// </summary>
    public async Task<SignedUrlResponse?> GetSignedUrlAsync(
        Guid              tenantId,
        Guid              appointmentId,
        Guid              attachmentId,
        Guid?             callerOrgId,
        string?           callerOrgType,
        bool              isAdmin,
        bool              isDownload,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        var attachments = await _attachments.GetByAppointmentAsync(tenantId, appointmentId, ct);
        var attachment  = attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException($"Attachment '{attachmentId}' was not found.");

        if (string.IsNullOrWhiteSpace(attachment.ExternalDocumentId))
            throw new InvalidOperationException("Attachment has no associated document in the Documents service.");

        // CC2-INT-B03: Enforce attachment scope before issuing a signed URL.
        EnforceScope(attachment, appointment, callerOrgId, callerOrgType, isAdmin);

        var result = await _documents.GetSignedUrlAsync(attachment.ExternalDocumentId, isDownload, ct);
        if (result is null) return null;

        return new SignedUrlResponse
        {
            Url              = result.RedeemUrl,
            ExpiresInSeconds = result.ExpiresInSeconds,
        };
    }

    /// <summary>
    /// CC2-INT-B03: Enforces scope rules for appointment attachments.
    /// Shared docs: caller must be an appointment participant (referring or receiving org) or admin.
    /// Provider-specific docs: caller must be from the receiving org (provider or law-firm org type) or admin.
    /// </summary>
    private static void EnforceScope(
        AppointmentAttachment attachment,
        Appointment           appointment,
        Guid?                 callerOrgId,
        string?               callerOrgType,
        bool                  isAdmin)
    {
        if (isAdmin) return;

        var scope = attachment.ExternalStorageProvider ?? AttachmentScope.Shared;

        if (string.Equals(scope, AttachmentScope.ProviderSpecific, StringComparison.OrdinalIgnoreCase))
        {
            var isAllowedOrgType =
                string.Equals(callerOrgType, "PROVIDER",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(callerOrgType, "LAW_FIRM",  StringComparison.OrdinalIgnoreCase);
            var isReceivingOrg = callerOrgId.HasValue && appointment.ReceivingOrganizationId == callerOrgId;
            if (!isAllowedOrgType || !isReceivingOrg)
                throw new UnauthorizedAccessException(
                    "Provider-specific documents are only accessible by the receiving organization or admins.");
        }
        else
        {
            var isParticipant =
                (callerOrgId.HasValue && appointment.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && appointment.ReceivingOrganizationId  == callerOrgId);
            if (!isParticipant)
                throw new UnauthorizedAccessException(
                    "Shared documents are only accessible by appointment participants.");
        }
    }

    private static void ValidateAttachmentRequest(CreateAttachmentMetadataRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FileName))
            errors["fileName"] = new[] { "FileName is required." };
        else if (request.FileName.Length > 500)
            errors["fileName"] = new[] { "FileName must not exceed 500 characters." };

        if (string.IsNullOrWhiteSpace(request.ContentType))
            errors["contentType"] = new[] { "ContentType is required." };
        else if (request.ContentType.Length > 200)
            errors["contentType"] = new[] { "ContentType must not exceed 200 characters." };

        if (request.FileSizeBytes < 0)
            errors["fileSizeBytes"] = new[] { "FileSizeBytes must be 0 or greater." };

        if (string.IsNullOrWhiteSpace(request.Status) || !AttachmentStatus.IsValid(request.Status))
            errors["status"] = new[] { $"'{request.Status}' is not a valid status. Allowed: {string.Join(", ", AttachmentStatus.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static AttachmentMetadataResponse ToResponse(AppointmentAttachment a) => new()
    {
        Id                      = a.Id,
        FileName                = a.FileName,
        ContentType             = a.ContentType,
        FileSizeBytes           = a.FileSizeBytes,
        ExternalDocumentId      = a.ExternalDocumentId,
        ExternalStorageProvider = a.ExternalStorageProvider,
        Status                  = a.Status,
        Notes                   = a.Notes,
        CreatedAtUtc            = a.CreatedAtUtc,
        CreatedByUserId         = a.CreatedByUserId
    };
}
