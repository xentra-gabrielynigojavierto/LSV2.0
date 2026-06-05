using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class ReferralAttachmentService : IReferralAttachmentService
{
    private readonly IReferralAttachmentRepository _attachments;
    private readonly IReferralRepository           _referrals;
    private readonly IDocumentServiceClient        _documents;

    public ReferralAttachmentService(
        IReferralAttachmentRepository attachments,
        IReferralRepository           referrals,
        IDocumentServiceClient        documents)
    {
        _attachments = attachments;
        _referrals   = referrals;
        _documents   = documents;
    }

    public async Task<List<AttachmentMetadataResponse>> GetByReferralAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrgId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        if (!isAdmin)
        {
            var isParticipant =
                (callerOrgId.HasValue && referral.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && referral.ReceivingOrganizationId  == callerOrgId);

            if (!isParticipant)
                throw new NotFoundException($"Referral '{referralId}' was not found.");
        }

        var rows = await _attachments.GetByReferralAsync(tenantId, referralId, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AttachmentMetadataResponse> CreateAsync(
        Guid tenantId,
        Guid referralId,
        Guid? userId,
        CreateAttachmentMetadataRequest request,
        CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        ValidateAttachmentRequest(request);

        var attachment = ReferralAttachment.Create(
            tenantId,
            referralId,
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
        Guid              referralId,
        Guid?             userId,
        Stream            fileContent,
        string            fileName,
        string            contentType,
        long              fileSizeBytes,
        UploadAttachmentRequest request,
        CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var uploadResult = await _documents.UploadAsync(
            fileContent,
            fileName,
            contentType,
            fileSizeBytes,
            tenantId,
            title:         fileName,
            referenceId:   referralId.ToString(),
            referenceType: "referral",
            ct:            ct);

        if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.DocumentId))
            throw new InvalidOperationException(
                $"Document upload failed: {uploadResult.Error ?? "unknown error"}");

        // CC2-INT-B03: ExternalStorageProvider stores the access scope ("shared" or "provider-specific").
        // EnforceScope reads this field to decide who may request a signed URL for this attachment.
        var attachment = ReferralAttachment.Create(
            tenantId,
            referralId,
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
    /// CC2-INT-B03: Enforces scope rules then fetches a short-lived signed URL from Documents.
    /// Shared docs: caller must be a referral participant.
    /// Provider-specific docs: caller must be from the receiving org or be an admin.
    /// </summary>
    public async Task<SignedUrlResponse?> GetSignedUrlAsync(
        Guid              tenantId,
        Guid              referralId,
        Guid              attachmentId,
        Guid?             callerOrgId,
        string?           callerOrgType,
        bool              isAdmin,
        bool              isDownload,
        CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var attachments = await _attachments.GetByReferralAsync(tenantId, referralId, ct);
        var attachment  = attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException($"Attachment '{attachmentId}' was not found.");

        if (string.IsNullOrWhiteSpace(attachment.ExternalDocumentId))
            throw new InvalidOperationException("Attachment has no associated document in the Documents service.");

        EnforceScope(attachment, referral, callerOrgId, callerOrgType, isAdmin);

        var result = await _documents.GetSignedUrlAsync(attachment.ExternalDocumentId, isDownload, ct);
        if (result is null) return null;

        return new SignedUrlResponse
        {
            Url              = result.RedeemUrl,
            ExpiresInSeconds = result.ExpiresInSeconds,
        };
    }

    private static void EnforceScope(
        ReferralAttachment attachment,
        Referral           referral,
        Guid?              callerOrgId,
        string?            callerOrgType,
        bool               isAdmin)
    {
        if (isAdmin) return;

        var scope = attachment.ExternalStorageProvider ?? AttachmentScope.Shared;

        if (string.Equals(scope, AttachmentScope.ProviderSpecific, StringComparison.OrdinalIgnoreCase))
        {
            // Allowed org types for provider-specific documents:
            //   - PROVIDER: standard healthcare/care provider org
            //   - LAW_FIRM: law-firm acting as receiving organization (task requirement)
            // Tenant/platform admins are already bypassed at the top of this method.
            var isAllowedOrgType =
                string.Equals(callerOrgType, "PROVIDER",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(callerOrgType, "LAW_FIRM",  StringComparison.OrdinalIgnoreCase);
            var isReceivingOrg = callerOrgId.HasValue && referral.ReceivingOrganizationId == callerOrgId;
            if (!isAllowedOrgType || !isReceivingOrg)
                throw new UnauthorizedAccessException(
                    "Provider-specific documents are only accessible by the receiving provider organization or admins.");
        }
        else
        {
            var isParticipant =
                (callerOrgId.HasValue && referral.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && referral.ReceivingOrganizationId  == callerOrgId);
            if (!isParticipant)
                throw new UnauthorizedAccessException(
                    "Shared documents are only accessible by referral participants.");
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

    private static AttachmentMetadataResponse ToResponse(ReferralAttachment a) => new()
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
