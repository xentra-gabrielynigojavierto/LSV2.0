using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class EmailTemplateConfig : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string TemplateKey { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? SubjectTemplate { get; private set; }
    public string? BodyTextTemplate { get; private set; }
    public string? BodyHtmlTemplate { get; private set; }
    public string TemplateScope { get; private set; } = Enums.TemplateScope.Tenant;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public int Version { get; private set; } = 1;

    private EmailTemplateConfig() { }

    public static EmailTemplateConfig Create(
        string templateKey,
        string displayName,
        string templateScope,
        Guid createdByUserId,
        Guid? tenantId = null,
        string? subjectTemplate = null,
        string? bodyTextTemplate = null,
        string? bodyHtmlTemplate = null,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (templateScope == Enums.TemplateScope.Tenant && (!tenantId.HasValue || tenantId == Guid.Empty))
            throw new ArgumentException("TenantId is required for tenant-scoped templates.");

        if (templateScope == Enums.TemplateScope.Global && tenantId.HasValue)
            throw new ArgumentException("Global templates must not have a TenantId.");

        var now = DateTime.UtcNow;
        return new EmailTemplateConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateKey = templateKey.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            SubjectTemplate = subjectTemplate?.Trim(),
            BodyTextTemplate = bodyTextTemplate,
            BodyHtmlTemplate = bodyHtmlTemplate,
            TemplateScope = templateScope,
            IsDefault = isDefault,
            IsActive = true,
            Version = 1,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string? displayName,
        string? subjectTemplate,
        string? bodyTextTemplate,
        string? bodyHtmlTemplate,
        bool? isDefault,
        bool? isActive,
        Guid updatedByUserId)
    {
        if (displayName is not null) DisplayName = displayName.Trim();
        if (subjectTemplate is not null) SubjectTemplate = subjectTemplate.Trim();
        if (bodyTextTemplate is not null) BodyTextTemplate = bodyTextTemplate;
        if (bodyHtmlTemplate is not null) BodyHtmlTemplate = bodyHtmlTemplate;
        if (isDefault.HasValue) IsDefault = isDefault.Value;
        if (isActive.HasValue) IsActive = isActive.Value;

        Version++;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string RenderSubject(Dictionary<string, string>? variables)
    {
        if (string.IsNullOrWhiteSpace(SubjectTemplate))
            return string.Empty;
        return RenderTemplate(SubjectTemplate, variables);
    }

    public string RenderBodyText(Dictionary<string, string>? variables)
    {
        if (string.IsNullOrWhiteSpace(BodyTextTemplate))
            return string.Empty;
        return RenderTemplate(BodyTextTemplate, variables);
    }

    public string RenderBodyHtml(Dictionary<string, string>? variables)
    {
        if (string.IsNullOrWhiteSpace(BodyHtmlTemplate))
            return string.Empty;
        return RenderTemplate(BodyHtmlTemplate, variables);
    }

    private static string RenderTemplate(string template, Dictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0)
            return template;

        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
