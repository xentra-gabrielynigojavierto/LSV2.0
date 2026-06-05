namespace Comms.Application.DTOs;

public record CreateEmailTemplateConfigRequest(
    string TemplateKey,
    string DisplayName,
    string TemplateScope,
    string? SubjectTemplate = null,
    string? BodyTextTemplate = null,
    string? BodyHtmlTemplate = null,
    bool IsDefault = false);

public record UpdateEmailTemplateConfigRequest(
    string? DisplayName = null,
    string? SubjectTemplate = null,
    string? BodyTextTemplate = null,
    string? BodyHtmlTemplate = null,
    bool? IsDefault = null,
    bool? IsActive = null);

public record EmailTemplateConfigResponse(
    Guid Id,
    Guid? TenantId,
    string TemplateKey,
    string DisplayName,
    string? SubjectTemplate,
    string? BodyTextTemplate,
    string? BodyHtmlTemplate,
    string TemplateScope,
    bool IsDefault,
    bool IsActive,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
