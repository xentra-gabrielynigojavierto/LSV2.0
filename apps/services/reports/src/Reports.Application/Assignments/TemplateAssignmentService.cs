using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Assignments.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Assignments;

public sealed class TemplateAssignmentService : ITemplateAssignmentService
{
    private static readonly HashSet<string> ValidScopes = new(StringComparer.OrdinalIgnoreCase) { "Global", "Tenant" };

    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly IAuditAdapter _audit;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<TemplateAssignmentService> _log;

    public TemplateAssignmentService(
        ITemplateAssignmentRepository assignmentRepo,
        ITemplateRepository templateRepo,
        IAuditAdapter audit,
        ICurrentTenantContext ctx,
        ILogger<TemplateAssignmentService> log)
    {
        _assignmentRepo = assignmentRepo;
        _templateRepo = templateRepo;
        _audit = audit;
        _ctx = ctx;
        _log = log;
    }

    public async Task<ServiceResult<TemplateAssignmentResponse>> CreateAssignmentAsync(
        Guid templateId, CreateTemplateAssignmentRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TemplateAssignmentResponse>.Forbidden("No authenticated user context.");

        var validation = ValidateCreateRequest(request);
        if (validation is not null)
            return ServiceResult<TemplateAssignmentResponse>.BadRequest(validation);

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateAssignmentResponse>.NotFound($"Template '{templateId}' not found.");

        var scope = request.AssignmentScope.Trim();
        var isGlobal = scope.Equals("Global", StringComparison.OrdinalIgnoreCase);

        if (isGlobal && request.TenantIds is { Count: > 0 })
            return ServiceResult<TemplateAssignmentResponse>.BadRequest("Global assignment must not include tenant IDs.");

        if (!isGlobal && (request.TenantIds is null || request.TenantIds.Count == 0))
            return ServiceResult<TemplateAssignmentResponse>.BadRequest("Tenant assignment must include at least one tenant ID.");

        if (!isGlobal && request.TenantIds is not null)
        {
            var normalized = request.TenantIds
                .Select(t => t?.Trim() ?? string.Empty)
                .Where(t => t.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return ServiceResult<TemplateAssignmentResponse>.BadRequest("Tenant assignment must include at least one non-blank tenant ID.");

            var duplicates = normalized.GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
                return ServiceResult<TemplateAssignmentResponse>.BadRequest(
                    $"Duplicate tenant IDs: {string.Join(", ", duplicates)}");

            request.TenantIds = normalized;
        }

        if (isGlobal)
        {
            var hasDuplicate = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, null, ct);
            if (hasDuplicate && request.IsActive)
                return ServiceResult<TemplateAssignmentResponse>.Conflict(
                    $"An active global assignment already exists for template '{templateId}'.");
        }
        else
        {
            foreach (var tenantId in request.TenantIds!)
            {
                var hasDuplicate = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId.Trim(), null, ct);
                if (hasDuplicate && request.IsActive)
                    return ServiceResult<TemplateAssignmentResponse>.Conflict(
                        $"An active assignment already exists for template '{templateId}' and tenant '{tenantId}'.");
            }
        }

        var assignment = new ReportTemplateAssignment
        {
            Id = Guid.NewGuid(),
            ReportTemplateId = templateId,
            AssignmentScope = isGlobal ? "Global" : "Tenant",
            ProductCode = template.ProductCode,
            OrganizationType = template.OrganizationType,
            IsActive = request.IsActive,
            RequiredFeatureCode = request.RequiredFeatureCode?.Trim(),
            MinimumTierCode = request.MinimumTierCode?.Trim(),
            CreatedByUserId = actorId
        };

        if (!isGlobal)
        {
            foreach (var tenantId in request.TenantIds!)
            {
                assignment.TenantTargets.Add(new ReportTemplateAssignmentTenant
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Trim(),
                    IsActive = true,
                    CreatedByUserId = actorId
                });
            }
        }

        ReportTemplateAssignment created;
        try
        {
            created = await _assignmentRepo.CreateAsync(assignment, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicting", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<TemplateAssignmentResponse>.Conflict(ex.Message);
        }

        await TryAuditAsync(AuditEventFactory.AssignmentCreated(
            "system", actorId, created.Id, templateId, created.AssignmentScope));

        _log.LogInformation("Assignment created: {AssignmentId} template={TemplateId} scope={Scope}",
            created.Id, templateId, created.AssignmentScope);

        return ServiceResult<TemplateAssignmentResponse>.Created(MapToResponse(created));
    }

    public async Task<ServiceResult<TemplateAssignmentResponse>> UpdateAssignmentAsync(
        Guid templateId, Guid assignmentId, UpdateTemplateAssignmentRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<TemplateAssignmentResponse>.Forbidden("No authenticated user context.");

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TemplateAssignmentResponse>.NotFound($"Template '{templateId}' not found.");

        var assignment = await _assignmentRepo.GetByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.ReportTemplateId != templateId)
            return ServiceResult<TemplateAssignmentResponse>.NotFound($"Assignment '{assignmentId}' not found for template '{templateId}'.");

        var isGlobal = assignment.AssignmentScope.Equals("Global", StringComparison.OrdinalIgnoreCase);

        if (!isGlobal && (request.TenantIds is null || request.TenantIds.Count == 0))
            return ServiceResult<TemplateAssignmentResponse>.BadRequest("Tenant assignment must include at least one tenant ID.");

        if (isGlobal && request.TenantIds is { Count: > 0 })
            return ServiceResult<TemplateAssignmentResponse>.BadRequest("Global assignment must not include tenant IDs.");

        if (!isGlobal && request.TenantIds is not null)
        {
            var normalized = request.TenantIds
                .Select(t => t?.Trim() ?? string.Empty)
                .Where(t => t.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return ServiceResult<TemplateAssignmentResponse>.BadRequest("Tenant assignment must include at least one non-blank tenant ID.");

            var duplicates = normalized.GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
                return ServiceResult<TemplateAssignmentResponse>.BadRequest(
                    $"Duplicate tenant IDs: {string.Join(", ", duplicates)}");

            request.TenantIds = normalized;
        }

        if (isGlobal && request.IsActive)
        {
            var hasDuplicate = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, assignmentId, ct);
            if (hasDuplicate)
                return ServiceResult<TemplateAssignmentResponse>.Conflict(
                    $"Another active global assignment already exists for template '{templateId}'.");
        }

        if (!isGlobal && request.IsActive && request.TenantIds is not null)
        {
            foreach (var tenantId in request.TenantIds)
            {
                var hasDuplicate = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId.Trim(), assignmentId, ct);
                if (hasDuplicate)
                    return ServiceResult<TemplateAssignmentResponse>.Conflict(
                        $"An active assignment already exists for template '{templateId}' and tenant '{tenantId}'.");
            }
        }

        assignment.IsActive = request.IsActive;
        assignment.RequiredFeatureCode = request.RequiredFeatureCode?.Trim();
        assignment.MinimumTierCode = request.MinimumTierCode?.Trim();
        assignment.UpdatedByUserId = actorId;

        if (!isGlobal && request.TenantIds is not null)
        {
            assignment.TenantTargets.Clear();
            foreach (var tenantId in request.TenantIds)
            {
                assignment.TenantTargets.Add(new ReportTemplateAssignmentTenant
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Trim(),
                    IsActive = true,
                    CreatedByUserId = actorId
                });
            }
        }

        ReportTemplateAssignment updated;
        try
        {
            updated = await _assignmentRepo.UpdateAsync(assignment, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicting", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<TemplateAssignmentResponse>.Conflict(ex.Message);
        }

        await TryAuditAsync(AuditEventFactory.AssignmentUpdated(
            "system", actorId, updated.Id, templateId));

        _log.LogInformation("Assignment updated: {AssignmentId} template={TemplateId}", updated.Id, templateId);

        return ServiceResult<TemplateAssignmentResponse>.Ok(MapToResponse(updated));
    }

    public async Task<ServiceResult<TemplateAssignmentResponse>> GetAssignmentByIdAsync(
        Guid templateId, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await _assignmentRepo.GetByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.ReportTemplateId != templateId)
            return ServiceResult<TemplateAssignmentResponse>.NotFound(
                $"Assignment '{assignmentId}' not found for template '{templateId}'.");

        return ServiceResult<TemplateAssignmentResponse>.Ok(MapToResponse(assignment));
    }

    public async Task<ServiceResult<IReadOnlyList<TemplateAssignmentResponse>>> ListAssignmentsAsync(
        Guid templateId, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<IReadOnlyList<TemplateAssignmentResponse>>.NotFound(
                $"Template '{templateId}' not found.");

        var assignments = await _assignmentRepo.ListByTemplateAsync(templateId, ct);
        var responses = assignments.Select(MapToResponse).ToList().AsReadOnly();
        return ServiceResult<IReadOnlyList<TemplateAssignmentResponse>>.Ok(responses);
    }

    public async Task<ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>> ResolveTenantCatalogAsync(
        TenantTemplateCatalogQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.TenantId))
            return ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>.BadRequest("TenantId is required.");
        if (string.IsNullOrWhiteSpace(query.ProductCode))
            return ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>.BadRequest("ProductCode is required.");
        if (string.IsNullOrWhiteSpace(query.OrganizationType))
            return ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>.BadRequest("OrganizationType is required.");

        var templates = await _assignmentRepo.ResolveTenantCatalogAsync(
            query.TenantId.Trim(), query.ProductCode.Trim(), query.OrganizationType.Trim(), ct);

        var items = new List<TenantTemplateCatalogItemResponse>();
        foreach (var t in templates)
        {
            var publishedVersion = t.Versions.FirstOrDefault(v => v.IsPublished);
            if (publishedVersion is null)
                continue;

            var assignment = t.Assignments.FirstOrDefault();
            items.Add(new TenantTemplateCatalogItemResponse
            {
                TemplateId = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                ProductCode = t.ProductCode,
                OrganizationType = t.OrganizationType,
                CurrentVersion = t.CurrentVersion,
                PublishedVersionNumber = publishedVersion.VersionNumber,
                AssignmentScope = assignment?.AssignmentScope ?? "Unknown",
                IsActive = t.IsActive
            });
        }

        await TryAuditAsync(AuditEventFactory.TenantCatalogResolved(
            query.TenantId.Trim(), query.ProductCode.Trim(), items.Count));

        return ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>.Ok(items.AsReadOnly());
    }

    private static string? ValidateCreateRequest(CreateTemplateAssignmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AssignmentScope))
            return "AssignmentScope is required.";
        if (!ValidScopes.Contains(request.AssignmentScope.Trim()))
            return $"AssignmentScope must be 'Global' or 'Tenant'. Got: '{request.AssignmentScope}'.";
        return null;
    }

    private static TemplateAssignmentResponse MapToResponse(ReportTemplateAssignment entity) => new()
    {
        AssignmentId = entity.Id,
        TemplateId = entity.ReportTemplateId,
        AssignmentScope = entity.AssignmentScope,
        ProductCode = entity.ProductCode,
        OrganizationType = entity.OrganizationType,
        IsActive = entity.IsActive,
        TenantIds = entity.TenantTargets
            .Where(t => t.IsActive)
            .Select(t => t.TenantId)
            .ToList(),
        RequiredFeatureCode = entity.RequiredFeatureCode,
        MinimumTierCode = entity.MinimumTierCode,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
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
