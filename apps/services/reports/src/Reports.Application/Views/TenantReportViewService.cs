using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Formulas;
using Reports.Application.Templates.DTOs;
using Reports.Application.Views.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Views;

public sealed class TenantReportViewService : ITenantReportViewService
{
    private readonly ITenantReportViewRepository _viewRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly IAuditAdapter _audit;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<TenantReportViewService> _log;

    public TenantReportViewService(
        ITenantReportViewRepository viewRepo,
        ITemplateRepository templateRepo,
        ITemplateAssignmentRepository assignmentRepo,
        IAuditAdapter audit,
        ICurrentTenantContext ctx,
        ILogger<TenantReportViewService> log)
    {
        _viewRepo = viewRepo;
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _audit = audit;
        _ctx = ctx;
        _log = log;
    }

    /// <summary>
    /// Resolves the current tenant ID from the JWT context.
    /// Returns null if the request is unauthenticated or has no tenant claim.
    /// </summary>
    private string? CurrentTenantId => _ctx.TenantId;

    public async Task<ServiceResult<TenantReportViewResponse>> CreateViewAsync(
        Guid templateId, CreateTenantReportViewRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TenantReportViewResponse>.Forbidden("No authenticated user context.");

        var validation = ValidateCreateRequest(request, templateId);
        if (validation is not null)
            return ServiceResult<TenantReportViewResponse>.BadRequest(validation);

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TenantReportViewResponse>.NotFound($"Template '{templateId}' not found.");

        if (!template.IsActive)
            return ServiceResult<TenantReportViewResponse>.BadRequest($"Template '{templateId}' is not active.");

        if (!string.IsNullOrWhiteSpace(request.FormulaConfigJson))
        {
            var formulaError = FormulaValidator.Validate(request.FormulaConfigJson);
            if (formulaError is not null)
                return ServiceResult<TenantReportViewResponse>.BadRequest($"Invalid formula config: {formulaError}");
        }

        if (request.IsDefault)
        {
            var hasDefault = await _viewRepo.HasDefaultViewAsync(request.TenantId.Trim(), templateId, null, ct);
            if (hasDefault)
            {
                var currentDefault = await _viewRepo.GetDefaultViewAsync(request.TenantId.Trim(), templateId, ct);
                if (currentDefault is not null)
                {
                    currentDefault.IsDefault = false;
                    await _viewRepo.UpdateAsync(currentDefault, ct);
                }
            }
        }

        var entity = new TenantReportView
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId.Trim(),
            ReportTemplateId = templateId,
            BaseTemplateVersionNumber = request.BaseTemplateVersionNumber,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsDefault = request.IsDefault,
            IsActive = true,
            LayoutConfigJson = request.LayoutConfigJson,
            ColumnConfigJson = request.ColumnConfigJson,
            FilterConfigJson = request.FilterConfigJson,
            FormulaConfigJson = request.FormulaConfigJson,
            FormattingConfigJson = request.FormattingConfigJson,
            CreatedByUserId = actorId
        };

        await _viewRepo.CreateAsync(entity, ct);

        await TryAuditAsync(AuditEventFactory.ViewCreated(
            entity.TenantId, actorId, entity.Id, templateId, entity.Name));

        _log.LogInformation("View created: {ViewId} tenant={TenantId} template={TemplateId} name={Name}",
            entity.Id, entity.TenantId, templateId, entity.Name);

        return ServiceResult<TenantReportViewResponse>.Created(MapToResponse(entity));
    }

    public async Task<ServiceResult<TenantReportViewResponse>> GetViewByIdAsync(
        Guid templateId, Guid viewId, CancellationToken ct)
    {
        var tenantId = CurrentTenantId;
        if (tenantId is null)
            return ServiceResult<TenantReportViewResponse>.Forbidden("No tenant context in request.");

        // Repository-layer: query includes TenantId filter — cross-tenant IDs return null
        var entity = await _viewRepo.GetByIdAsync(viewId, tenantId, ct);
        if (entity is null || entity.ReportTemplateId != templateId)
            return ServiceResult<TenantReportViewResponse>.NotFound($"View '{viewId}' not found for template '{templateId}'.");

        // Service-layer: explicit ownership check as defence-in-depth
        if (!string.Equals(entity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning(
                "Tenant ownership violation on GetView: viewId={ViewId} requestTenant={RequestTenantId} ownerTenant={OwnerTenantId} userId={UserId}",
                viewId, tenantId, entity.TenantId, _ctx.UserId);
            return ServiceResult<TenantReportViewResponse>.Forbidden("Access denied.");
        }

        return ServiceResult<TenantReportViewResponse>.Ok(MapToResponse(entity));
    }

    public async Task<ServiceResult<TenantReportViewResponse>> UpdateViewAsync(
        Guid templateId, Guid viewId, UpdateTenantReportViewRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TenantReportViewResponse>.Forbidden("No authenticated user context.");

        var tenantId = CurrentTenantId;
        if (tenantId is null)
            return ServiceResult<TenantReportViewResponse>.Forbidden("No tenant context in request.");

        // Repository-layer: query includes TenantId filter — cross-tenant IDs return null
        var entity = await _viewRepo.GetByIdAsync(viewId, tenantId, ct);
        if (entity is null || entity.ReportTemplateId != templateId)
            return ServiceResult<TenantReportViewResponse>.NotFound($"View '{viewId}' not found for template '{templateId}'.");

        // Service-layer: explicit ownership check as defence-in-depth
        if (!string.Equals(entity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning(
                "Tenant ownership violation on UpdateView: viewId={ViewId} requestTenant={RequestTenantId} ownerTenant={OwnerTenantId} userId={UserId}",
                viewId, tenantId, entity.TenantId, _ctx.UserId);
            return ServiceResult<TenantReportViewResponse>.Forbidden("Access denied.");
        }

        if (!string.IsNullOrWhiteSpace(request.FormulaConfigJson))
        {
            var formulaError = FormulaValidator.Validate(request.FormulaConfigJson);
            if (formulaError is not null)
                return ServiceResult<TenantReportViewResponse>.BadRequest($"Invalid formula config: {formulaError}");
        }

        // Default view flip: scoped to current tenant only
        if (request.IsDefault == true && !entity.IsDefault)
        {
            var currentDefault = await _viewRepo.GetDefaultViewAsync(tenantId, templateId, ct);
            if (currentDefault is not null && currentDefault.Id != viewId)
            {
                currentDefault.IsDefault = false;
                await _viewRepo.UpdateAsync(currentDefault, ct);
            }
        }

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.Description is not null) entity.Description = request.Description.Trim();
        if (request.IsDefault.HasValue) entity.IsDefault = request.IsDefault.Value;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.LayoutConfigJson is not null) entity.LayoutConfigJson = request.LayoutConfigJson;
        if (request.ColumnConfigJson is not null) entity.ColumnConfigJson = request.ColumnConfigJson;
        if (request.FilterConfigJson is not null) entity.FilterConfigJson = request.FilterConfigJson;
        if (request.FormulaConfigJson is not null) entity.FormulaConfigJson = request.FormulaConfigJson;
        if (request.FormattingConfigJson is not null) entity.FormattingConfigJson = request.FormattingConfigJson;
        entity.UpdatedByUserId = actorId;

        await _viewRepo.UpdateAsync(entity, ct);

        await TryAuditAsync(AuditEventFactory.ViewUpdated(
            entity.TenantId, actorId, entity.Id, templateId, entity.Name));

        return ServiceResult<TenantReportViewResponse>.Ok(MapToResponse(entity));
    }

    public async Task<ServiceResult<IReadOnlyList<TenantReportViewResponse>>> ListViewsAsync(
        Guid templateId, string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<IReadOnlyList<TenantReportViewResponse>>.BadRequest("TenantId is required.");

        var entities = await _viewRepo.ListByTenantAndTemplateAsync(tenantId.Trim(), templateId, ct);
        var responses = entities.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<TenantReportViewResponse>>.Ok(responses);
    }

    public async Task<ServiceResult<TenantReportViewResponse>> DeleteViewAsync(
        Guid templateId, Guid viewId, CancellationToken ct)
    {
        var tenantId = CurrentTenantId;
        if (tenantId is null)
            return ServiceResult<TenantReportViewResponse>.Forbidden("No tenant context in request.");

        // Repository-layer: query includes TenantId filter — cross-tenant IDs return null
        var entity = await _viewRepo.GetByIdAsync(viewId, tenantId, ct);
        if (entity is null || entity.ReportTemplateId != templateId)
            return ServiceResult<TenantReportViewResponse>.NotFound($"View '{viewId}' not found for template '{templateId}'.");

        // Service-layer: explicit ownership check as defence-in-depth
        if (!string.Equals(entity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning(
                "Tenant ownership violation on DeleteView: viewId={ViewId} requestTenant={RequestTenantId} ownerTenant={OwnerTenantId} userId={UserId}",
                viewId, tenantId, entity.TenantId, _ctx.UserId);
            return ServiceResult<TenantReportViewResponse>.Forbidden("Access denied.");
        }

        var response = MapToResponse(entity);
        // Repository-layer delete also scoped to tenantId for defence-in-depth
        await _viewRepo.DeleteAsync(viewId, tenantId, ct);

        await TryAuditAsync(AuditEventFactory.ViewDeleted(
            entity.TenantId, entity.UpdatedByUserId ?? entity.CreatedByUserId, entity.Id, templateId, entity.Name));

        _log.LogInformation("View deleted: {ViewId} tenant={TenantId} template={TemplateId}",
            viewId, entity.TenantId, templateId);

        return ServiceResult<TenantReportViewResponse>.Ok(response);
    }

    private static TenantReportViewResponse MapToResponse(TenantReportView entity) => new()
    {
        ViewId = entity.Id,
        TenantId = entity.TenantId,
        ReportTemplateId = entity.ReportTemplateId,
        BaseTemplateVersionNumber = entity.BaseTemplateVersionNumber,
        Name = entity.Name,
        Description = entity.Description,
        IsDefault = entity.IsDefault,
        IsActive = entity.IsActive,
        LayoutConfigJson = entity.LayoutConfigJson,
        ColumnConfigJson = entity.ColumnConfigJson,
        FilterConfigJson = entity.FilterConfigJson,
        FormulaConfigJson = entity.FormulaConfigJson,
        FormattingConfigJson = entity.FormattingConfigJson,
        CreatedAtUtc = entity.CreatedAtUtc,
        CreatedByUserId = entity.CreatedByUserId,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        UpdatedByUserId = entity.UpdatedByUserId
    };

    private static string? ValidateCreateRequest(CreateTenantReportViewRequest request, Guid templateId)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId)) return "TenantId is required.";
        if (request.ReportTemplateId == Guid.Empty || request.ReportTemplateId != templateId)
            return "ReportTemplateId must match the route templateId.";
        if (request.BaseTemplateVersionNumber <= 0) return "BaseTemplateVersionNumber must be > 0.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (request.Name.Length > 200) return "Name must be 200 characters or fewer.";
        return null;
    }

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
