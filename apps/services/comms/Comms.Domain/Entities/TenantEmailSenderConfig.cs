using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class TenantEmailSenderConfig : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string FromEmail { get; private set; } = string.Empty;
    public string? ReplyToEmail { get; private set; }
    public string SenderType { get; private set; } = Enums.SenderType.NoReply;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public string VerificationStatus { get; private set; } = Enums.VerificationStatus.Pending;
    public bool AllowedForSharedExternal { get; private set; } = true;

    private TenantEmailSenderConfig() { }

    public static TenantEmailSenderConfig Create(
        Guid tenantId,
        string displayName,
        string fromEmail,
        string senderType,
        Guid createdByUserId,
        string? replyToEmail = null,
        bool isDefault = false,
        bool allowedForSharedExternal = true,
        string? verificationStatus = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromEmail);
        if (!Enums.SenderType.IsValid(senderType))
            throw new ArgumentException($"Invalid sender type: {senderType}", nameof(senderType));

        var now = DateTime.UtcNow;
        return new TenantEmailSenderConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = displayName.Trim(),
            FromEmail = fromEmail.Trim().ToLowerInvariant(),
            ReplyToEmail = replyToEmail?.Trim().ToLowerInvariant(),
            SenderType = senderType,
            IsDefault = isDefault,
            IsActive = true,
            VerificationStatus = verificationStatus ?? Enums.VerificationStatus.Pending,
            AllowedForSharedExternal = allowedForSharedExternal,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string? displayName,
        string? replyToEmail,
        string? senderType,
        bool? isDefault,
        bool? isActive,
        string? verificationStatus,
        bool? allowedForSharedExternal,
        Guid updatedByUserId)
    {
        if (displayName is not null) DisplayName = displayName.Trim();
        if (replyToEmail is not null) ReplyToEmail = replyToEmail.Trim().ToLowerInvariant();
        if (senderType is not null)
        {
            if (!Enums.SenderType.IsValid(senderType))
                throw new ArgumentException($"Invalid sender type: {senderType}");
            SenderType = senderType;
        }
        if (isDefault.HasValue) IsDefault = isDefault.Value;
        if (isActive.HasValue) IsActive = isActive.Value;
        if (verificationStatus is not null) VerificationStatus = verificationStatus;
        if (allowedForSharedExternal.HasValue) AllowedForSharedExternal = allowedForSharedExternal.Value;

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool CanSend() =>
        IsActive && Enums.VerificationStatus.IsUsable(VerificationStatus);

    public void ClearDefault(Guid updatedByUserId)
    {
        IsDefault = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
