namespace Comms.Application.DTOs;

public record CreateTenantEmailSenderConfigRequest(
    string DisplayName,
    string FromEmail,
    string SenderType,
    string? ReplyToEmail = null,
    bool IsDefault = false,
    bool AllowedForSharedExternal = true,
    string? VerificationStatus = null);

public record UpdateTenantEmailSenderConfigRequest(
    string? DisplayName = null,
    string? ReplyToEmail = null,
    string? SenderType = null,
    bool? IsDefault = null,
    bool? IsActive = null,
    string? VerificationStatus = null,
    bool? AllowedForSharedExternal = null);

public record TenantEmailSenderConfigResponse(
    Guid Id,
    Guid TenantId,
    string DisplayName,
    string FromEmail,
    string? ReplyToEmail,
    string SenderType,
    bool IsDefault,
    bool IsActive,
    string VerificationStatus,
    bool AllowedForSharedExternal,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
