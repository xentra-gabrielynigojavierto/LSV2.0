using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class ServicingItemService : IServicingItemService
{
    private readonly IServicingItemRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ServicingItemService> _logger;

    public ServicingItemService(
        IServicingItemRepository repo,
        IAuditPublisher audit,
        ILogger<ServicingItemService> logger)
    {
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PaginatedResult<ServicingItemResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? priority, string? assignedTo,
        Guid? caseId, Guid? lienId, int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _repo.SearchAsync(
            tenantId, search, status, priority, assignedTo, caseId, lienId, page, pageSize, ct);

        return new PaginatedResult<ServicingItemResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ServicingItemResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<ServicingItemResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateServicingItemRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.TaskNumber))
            errors.Add("taskNumber", ["Task number is required."]);
        if (string.IsNullOrWhiteSpace(request.TaskType))
            errors.Add("taskType", ["Task type is required."]);
        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("description", ["Description is required."]);
        if (string.IsNullOrWhiteSpace(request.AssignedTo))
            errors.Add("assignedTo", ["Assignee is required."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing.", errors);

        var existing = await _repo.GetByTaskNumberAsync(tenantId, request.TaskNumber.Trim(), ct);
        if (existing is not null)
            throw new ConflictException(
                $"A servicing task with number '{request.TaskNumber.Trim()}' already exists.",
                "TASK_NUMBER_DUPLICATE");

        ServicingItem entity;
        try
        {
            entity = ServicingItem.Create(
                tenantId: tenantId,
                orgId: orgId,
                taskNumber: request.TaskNumber,
                taskType: request.TaskType,
                description: request.Description,
                assignedTo: request.AssignedTo,
                createdByUserId: actingUserId,
                priority: request.Priority,
                caseId: request.CaseId,
                lienId: request.LienId,
                dueDate: request.DueDate,
                notes: request.Notes,
                assignedToUserId: request.AssignedToUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "value"] = [ex.Message] });
        }

        await _repo.AddAsync(entity, ct);

        _logger.LogInformation(
            "ServicingItem created: {TaskId} TaskNumber={TaskNumber} Tenant={TenantId}",
            entity.Id, entity.TaskNumber, tenantId);

        _audit.Publish(
            eventType: "liens.servicing.created",
            action: "create",
            description: $"Servicing task '{entity.TaskNumber}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "ServicingItem",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<ServicingItemResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateServicingItemRequest request, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Servicing item '{id}' not found for tenant '{tenantId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.TaskType))
            errors.Add("taskType", ["Task type is required."]);
        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("description", ["Description is required."]);
        if (string.IsNullOrWhiteSpace(request.AssignedTo))
            errors.Add("assignedTo", ["Assignee is required."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        try
        {
            entity.Update(
                taskType: request.TaskType,
                description: request.Description,
                assignedTo: request.AssignedTo,
                updatedByUserId: actingUserId,
                priority: request.Priority,
                caseId: request.CaseId,
                lienId: request.LienId,
                dueDate: request.DueDate,
                notes: request.Notes,
                assignedToUserId: request.AssignedToUserId);

            if (request.Status is not null && request.Status != entity.Status)
                entity.TransitionStatus(request.Status, actingUserId, request.Resolution);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message,
                new Dictionary<string, string[]> { [ex.ParamName ?? "value"] = [ex.Message] });
        }

        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "ServicingItem updated: {TaskId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.servicing.updated",
            action: "update",
            description: $"Servicing task '{entity.TaskNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "ServicingItem",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<ServicingItemResponse> UpdateStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        string status, string? resolution = null, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Servicing item '{id}' not found for tenant '{tenantId}'.");

        if (!ServicingStatus.All.Contains(status))
            throw new ValidationException($"Invalid status: '{status}'. Valid values: {string.Join(", ", ServicingStatus.All)}",
                new Dictionary<string, string[]> { ["status"] = [$"Invalid status: '{status}'."] });

        entity.TransitionStatus(status, actingUserId, resolution);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "ServicingItem status updated: {TaskId} -> {Status} Tenant={TenantId}",
            entity.Id, status, tenantId);

        _audit.Publish(
            eventType: "liens.servicing.status_changed",
            action: "update",
            description: $"Servicing task '{entity.TaskNumber}' status changed to '{status}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "ServicingItem",
            entityId: entity.Id.ToString());

        return MapToResponse(entity);
    }

    private static ServicingItemResponse MapToResponse(ServicingItem entity)
    {
        return new ServicingItemResponse
        {
            Id = entity.Id,
            TaskNumber = entity.TaskNumber,
            TaskType = entity.TaskType,
            Description = entity.Description,
            Status = entity.Status,
            Priority = entity.Priority,
            AssignedTo = entity.AssignedTo,
            AssignedToUserId = entity.AssignedToUserId,
            CaseId = entity.CaseId,
            LienId = entity.LienId,
            DueDate = entity.DueDate,
            Notes = entity.Notes,
            Resolution = entity.Resolution,
            StartedAtUtc = entity.StartedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            EscalatedAtUtc = entity.EscalatedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}
