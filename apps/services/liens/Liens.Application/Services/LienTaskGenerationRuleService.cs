using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskGenerationRuleService : ILienTaskGenerationRuleService
{
    private readonly ILienTaskGenerationRuleRepository _repo;
    private readonly IAuditPublisher                   _audit;
    private readonly ILogger<LienTaskGenerationRuleService> _logger;

    public LienTaskGenerationRuleService(
        ILienTaskGenerationRuleRepository repo,
        IAuditPublisher audit,
        ILogger<LienTaskGenerationRuleService> logger)
    {
        _repo   = repo;
        _audit  = audit;
        _logger = logger;
    }

    public async Task<List<TaskGenerationRuleResponse>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var list = await _repo.GetByTenantAsync(tenantId, ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<TaskGenerationRuleResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<TaskGenerationRuleResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateTaskGenerationRuleRequest request,
        CancellationToken ct = default)
    {
        var errors = Validate(request.Name, request.EventType, request.TaskTemplateId,
            request.ContextType, request.DuplicatePreventionMode, request.AssignmentMode,
            request.DueDateMode, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        var entity = LienTaskGenerationRule.Create(
            tenantId:                  tenantId,
            name:                      request.Name,
            eventType:                 request.EventType,
            taskTemplateId:            request.TaskTemplateId,
            contextType:               request.ContextType,
            updateSource:              request.UpdateSource,
            createdByUserId:           actingUserId,
            description:               request.Description,
            applicableWorkflowStageId: request.ApplicableWorkflowStageId,
            duplicatePreventionMode:   request.DuplicatePreventionMode,
            assignmentMode:            request.AssignmentMode,
            dueDateMode:               request.DueDateMode,
            dueDateOffsetDays:         request.DueDateOffsetDays,
            updatedByName:             request.UpdatedByName);

        await _repo.AddAsync(entity, ct);

        _logger.LogInformation("TaskGenerationRule created: {RuleId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_generation_rule.created",
            action:      "create",
            description: $"Task generation rule '{entity.Name}' created from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGenerationRule",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<TaskGenerationRuleResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskGenerationRuleRequest request,
        CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Task generation rule '{id}' not found.");

        var errors = Validate(request.Name, request.EventType, request.TaskTemplateId,
            request.ContextType, request.DuplicatePreventionMode, request.AssignmentMode,
            request.DueDateMode, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        entity.Update(
            name:                      request.Name,
            description:               request.Description,
            eventType:                 request.EventType,
            taskTemplateId:            request.TaskTemplateId,
            contextType:               request.ContextType,
            applicableWorkflowStageId: request.ApplicableWorkflowStageId,
            duplicatePreventionMode:   request.DuplicatePreventionMode,
            assignmentMode:            request.AssignmentMode,
            dueDateMode:               request.DueDateMode,
            dueDateOffsetDays:         request.DueDateOffsetDays,
            updateSource:              request.UpdateSource,
            updatedByUserId:           actingUserId,
            expectedVersion:           request.Version,
            updatedByName:             request.UpdatedByName);

        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_generation_rule.updated",
            action:      "update",
            description: $"Task generation rule '{entity.Name}' updated from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGenerationRule",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<TaskGenerationRuleResponse> ActivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateRuleRequest request,
        CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Task generation rule '{id}' not found.");

        entity.Activate(actingUserId, request.UpdateSource, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_generation_rule.activated",
            action:      "activate",
            description: $"Task generation rule '{entity.Name}' activated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGenerationRule",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<TaskGenerationRuleResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateRuleRequest request,
        CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Task generation rule '{id}' not found.");

        entity.Deactivate(actingUserId, request.UpdateSource, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.task_generation_rule.deactivated",
            action:      "deactivate",
            description: $"Task generation rule '{entity.Name}' deactivated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGenerationRule",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static Dictionary<string, string[]> Validate(
        string name, string eventType, Guid templateId,
        string contextType, string duplicateMode, string assignmentMode,
        string dueDateMode, string updateSource)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = ["Name is required."];
        if (string.IsNullOrWhiteSpace(eventType) || !TaskGenerationEventType.All.Contains(eventType))
            errors["eventType"] = [$"Invalid eventType '{eventType}'. Valid: {string.Join(", ", TaskGenerationEventType.All)}"];
        if (templateId == Guid.Empty)
            errors["taskTemplateId"] = ["TaskTemplateId is required."];
        if (string.IsNullOrWhiteSpace(contextType) || !TaskTemplateContextType.All.Contains(contextType))
            errors["contextType"] = [$"Invalid contextType '{contextType}'."];
        if (string.IsNullOrWhiteSpace(duplicateMode) || !DuplicatePreventionMode.All.Contains(duplicateMode))
            errors["duplicatePreventionMode"] = [$"Invalid duplicatePreventionMode '{duplicateMode}'."];
        if (string.IsNullOrWhiteSpace(assignmentMode) || !AssignmentMode.All.Contains(assignmentMode))
            errors["assignmentMode"] = [$"Invalid assignmentMode '{assignmentMode}'."];
        if (string.IsNullOrWhiteSpace(dueDateMode) || !DueDateMode.All.Contains(dueDateMode))
            errors["dueDateMode"] = [$"Invalid dueDateMode '{dueDateMode}'."];
        if (string.IsNullOrWhiteSpace(updateSource) || !WorkflowUpdateSources.All.Contains(updateSource))
            errors["updateSource"] = [$"Invalid updateSource '{updateSource}'."];
        return errors;
    }

    private static TaskGenerationRuleResponse MapToResponse(LienTaskGenerationRule r) =>
        new()
        {
            Id                        = r.Id,
            TenantId                  = r.TenantId,
            ProductCode               = r.ProductCode,
            Name                      = r.Name,
            Description               = r.Description,
            EventType                 = r.EventType,
            TaskTemplateId            = r.TaskTemplateId,
            ContextType               = r.ContextType,
            ApplicableWorkflowStageId = r.ApplicableWorkflowStageId,
            DuplicatePreventionMode   = r.DuplicatePreventionMode,
            AssignmentMode            = r.AssignmentMode,
            DueDateMode               = r.DueDateMode,
            DueDateOffsetDays         = r.DueDateOffsetDays,
            IsActive                  = r.IsActive,
            Version                   = r.Version,
            LastUpdatedAt             = r.LastUpdatedAt,
            LastUpdatedByUserId       = r.LastUpdatedByUserId,
            LastUpdatedByName         = r.LastUpdatedByName,
            LastUpdatedSource         = r.LastUpdatedSource,
            CreatedByUserId           = r.CreatedByUserId,
            CreatedAtUtc              = r.CreatedAtUtc,
            UpdatedAtUtc              = r.UpdatedAtUtc,
        };
}
