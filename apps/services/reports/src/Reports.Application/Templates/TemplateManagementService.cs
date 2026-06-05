using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Templates;

public sealed class TemplateManagementService : ITemplateManagementService
{
    private readonly ITemplateRepository _repo;
    private readonly IAuditAdapter _audit;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<TemplateManagementService> _log;

    public TemplateManagementService(
        ITemplateRepository repo,
        IAuditAdapter audit,
        ICurrentTenantContext ctx,
        ILogger<TemplateManagementService> log)
    {
        _repo = repo;
        _audit = audit;
        _ctx = ctx;
        _log = log;
    }

    public async Task<ServiceResult<TemplateResponse>> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct)
    {
        var actorId = _ctx.UserId ?? "system";

        var validation = ValidateCreateRequest(request);
        if (validation is not null)
            return ServiceResult<TemplateResponse>.BadRequest(validation);

        var existing = await _repo.GetByCodeAsync(request.Code, ct);
        if (existing is not null)
            return ServiceResult<TemplateResponse>.Conflict($"A template with code '{request.Code}' already exists.");

        var entity = new ReportTemplate
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ProductCode = request.ProductCode.Trim(),
            OrganizationType = request.OrganizationType.Trim(),
            IsActive = request.IsActive,
            CurrentVersion = 0
        };

        var created = await _repo.CreateAsync(entity, ct);

        await TryAuditAsync(AuditEventFactory.TemplateCreated(
            "system", actorId, created.Id, created.Code, created.ProductCode));

        _log.LogInformation("Template created: {TemplateId} code={Code}", created.Id, created.Code);
        return ServiceResult<TemplateResponse>.Created(MapToResponse(created));
    }

    public async Task<ServiceResult<TemplateResponse>> UpdateTemplateAsync(Guid templateId, UpdateTemplateRequest request, CancellationToken ct)
    {
        var actorId = _ctx.UserId ?? "system";

        var validation = ValidateUpdateRequest(request);
        if (validation is not null)
            return ServiceResult<TemplateResponse>.BadRequest(validation);

        var entity = await _repo.GetByIdAsync(templateId, ct);
        if (entity is null)
            return ServiceResult<TemplateResponse>.NotFound($"Template '{templateId}' not found.");

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.ProductCode = request.ProductCode.Trim();
        entity.OrganizationType = request.OrganizationType.Trim();
        entity.IsActive = request.IsActive;

        var updated = await _repo.UpdateAsync(entity, ct);

        await TryAuditAsync(AuditEventFactory.TemplateUpdated(
            "system", actorId, updated.Id, updated.Code, updated.ProductCode));

        _log.LogInformation("Template updated: {TemplateId}", updated.Id);
        return ServiceResult<TemplateResponse>.Ok(MapToResponse(updated));
    }

    public async Task<ServiceResult<TemplateResponse>> GetTemplateByIdAsync(Guid templateId, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(templateId, ct);
        if (entity is null)
            return ServiceResult<TemplateResponse>.NotFound($"Template '{templateId}' not found.");

        return ServiceResult<TemplateResponse>.Ok(MapToResponse(entity));
    }

    public async Task<ServiceResult<IReadOnlyList<TemplateResponse>>> ListTemplatesAsync(string? productCode, string? organizationType, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var entities = await _repo.ListAsync(productCode, organizationType, activeOnly: true, page, pageSize, ct);
        var responses = entities.Select(MapToResponse).ToList().AsReadOnly();
        return ServiceResult<IReadOnlyList<TemplateResponse>>.Ok(responses);
    }

    public async Task<ServiceResult<TemplateVersionResponse>> CreateVersionAsync(Guid templateId, CreateTemplateVersionRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TemplateVersionResponse>.Forbidden("No authenticated user context.");

        var validation = ValidateCreateVersionRequest(request);
        if (validation is not null)
            return ServiceResult<TemplateVersionResponse>.BadRequest(validation);

        var template = await _repo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"Template '{templateId}' not found.");

        var version = new ReportTemplateVersion
        {
            ReportTemplateId = templateId,
            TemplateBody = request.TemplateBody,
            OutputFormat = request.OutputFormat.Trim(),
            ChangeNotes = request.ChangeNotes?.Trim(),
            IsActive = request.IsActive,
            CreatedByUserId = actorId
        };

        var created = await _repo.CreateVersionAtomicAsync(template, version, ct);

        await TryAuditAsync(AuditEventFactory.VersionCreated(
            "system", actorId, templateId, template.Code, created.VersionNumber, template.ProductCode));

        _log.LogInformation("Version {Version} created for template {TemplateId}", created.VersionNumber, templateId);
        return ServiceResult<TemplateVersionResponse>.Created(MapToVersionResponse(created));
    }

    public async Task<ServiceResult<TemplateVersionResponse>> GetLatestVersionAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"Template '{templateId}' not found.");

        var version = await _repo.GetLatestVersionAsync(templateId, ct);
        if (version is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"No versions found for template '{templateId}'.");

        return ServiceResult<TemplateVersionResponse>.Ok(MapToVersionResponse(version));
    }

    public async Task<ServiceResult<TemplateVersionResponse>> GetPublishedVersionAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"Template '{templateId}' not found.");

        var version = await _repo.GetPublishedVersionAsync(templateId, ct);
        if (version is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"No published version found for template '{templateId}'.");

        return ServiceResult<TemplateVersionResponse>.Ok(MapToVersionResponse(version));
    }

    public async Task<ServiceResult<TemplateVersionResponse>> PublishVersionAsync(Guid templateId, int versionNumber, PublishTemplateVersionRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TemplateVersionResponse>.Forbidden("No authenticated user context.");

        var template = await _repo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"Template '{templateId}' not found.");

        var targetVersion = await _repo.GetVersionAsync(templateId, versionNumber, ct);
        if (targetVersion is null)
            return ServiceResult<TemplateVersionResponse>.NotFound($"Version {versionNumber} not found for template '{templateId}'.");

        if (targetVersion.IsPublished)
            return ServiceResult<TemplateVersionResponse>.Ok(MapToVersionResponse(targetVersion));

        var updated = await _repo.PublishVersionAtomicAsync(templateId, versionNumber, actorId, ct);

        await TryAuditAsync(AuditEventFactory.VersionPublished(
            "system", actorId, templateId, template.Code, versionNumber, template.ProductCode));

        _log.LogInformation("Version {Version} published for template {TemplateId}", versionNumber, templateId);
        return ServiceResult<TemplateVersionResponse>.Ok(MapToVersionResponse(updated));
    }

    public async Task<ServiceResult<IReadOnlyList<TemplateVersionResponse>>> ListVersionsAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<IReadOnlyList<TemplateVersionResponse>>.NotFound($"Template '{templateId}' not found.");

        var versions = await _repo.ListVersionsAsync(templateId, ct);
        var responses = versions.Select(MapToVersionResponse).ToList().AsReadOnly();
        return ServiceResult<IReadOnlyList<TemplateVersionResponse>>.Ok(responses);
    }

    private static string? ValidateCreateRequest(CreateTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return "Code is required.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode)) return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType)) return "OrganizationType is required.";
        return null;
    }

    private static string? ValidateUpdateRequest(UpdateTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode)) return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType)) return "OrganizationType is required.";
        return null;
    }

    private static string? ValidateCreateVersionRequest(CreateTemplateVersionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateBody)) return "TemplateBody is required.";
        if (string.IsNullOrWhiteSpace(request.OutputFormat)) return "OutputFormat is required.";
        return null;
    }

    private static TemplateResponse MapToResponse(ReportTemplate entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        ProductCode = entity.ProductCode,
        OrganizationType = entity.OrganizationType,
        IsActive = entity.IsActive,
        CurrentVersion = entity.CurrentVersion,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };

    private static TemplateVersionResponse MapToVersionResponse(ReportTemplateVersion entity) => new()
    {
        Id = entity.Id,
        TemplateId = entity.ReportTemplateId,
        VersionNumber = entity.VersionNumber,
        TemplateBody = entity.TemplateBody,
        OutputFormat = entity.OutputFormat,
        ChangeNotes = entity.ChangeNotes,
        IsActive = entity.IsActive,
        IsPublished = entity.IsPublished,
        PublishedAtUtc = entity.PublishedAtUtc,
        CreatedAtUtc = entity.CreatedAtUtc,
        CreatedByUserId = entity.CreatedByUserId
    };

    private async Task TryAuditAsync(Reports.Contracts.Audit.AuditEventDto auditEvent)
    {
        try
        {
            await _audit.RecordEventAsync(auditEvent);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit hook failed for action {Action}", auditEvent.EventType);
        }
    }
}
