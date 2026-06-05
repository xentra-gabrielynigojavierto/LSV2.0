using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskStageService : ITaskStageService
{
    private readonly ITaskStageRepository    _stages;
    private readonly IUnitOfWork             _uow;
    private readonly ILogger<TaskStageService> _logger;

    public TaskStageService(
        ITaskStageRepository    stages,
        IUnitOfWork             uow,
        ILogger<TaskStageService> logger)
    {
        _stages = stages;
        _uow    = uow;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<TaskStageDto> CreateAsync(
        Guid tenantId, Guid userId, CreateTaskStageRequest request, CancellationToken ct = default)
    {
        // TASK-B05 (TASK-014) — validate product code against canonical registry
        var productCode = KnownProductCodes.ValidateOptional(request.SourceProductCode);

        var stage = TaskStageConfig.Create(
            tenantId, request.Code, request.Name, request.DisplayOrder,
            userId, productCode);

        await _stages.AddAsync(stage, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Stage config {StageId} ({Code}) created for tenant {TenantId}", stage.Id, stage.Code, tenantId);

        return TaskStageDto.From(stage);
    }

    public async System.Threading.Tasks.Task<TaskStageDto> UpdateAsync(
        Guid tenantId, Guid id, Guid userId, UpdateTaskStageRequest request, CancellationToken ct = default)
    {
        var stage = await _stages.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Stage config {id} not found.");

        stage.Update(request.Name, request.DisplayOrder, request.IsActive, userId, request.ProductSettingsJson);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Stage config {StageId} updated by {UserId} in tenant {TenantId}", id, userId, tenantId);

        return TaskStageDto.From(stage);
    }

    public async System.Threading.Tasks.Task<TaskStageDto> UpsertFromSourceAsync(
        Guid tenantId, Guid userId, UpsertFromSourceStageRequest request, CancellationToken ct = default)
    {
        var productCode = KnownProductCodes.ValidateOptional(request.SourceProductCode)
                          ?? throw new ArgumentException($"Unknown product code '{request.SourceProductCode}'.", nameof(request.SourceProductCode));

        var code = request.Id.ToString("N").ToUpperInvariant();

        var existing = await _stages.GetByIdAnyTenantAsync(request.Id, ct);

        if (existing is null)
        {
            var stage = TaskStageConfig.Create(
                tenantId:           tenantId,
                code:               code,
                name:               request.Name,
                displayOrder:       request.DisplayOrder,
                createdByUserId:    userId,
                sourceProductCode:  productCode,
                productSettingsJson: request.ProductSettingsJson,
                id:                 request.Id);

            await _stages.AddAsync(stage, ct);

            if (!request.IsActive)
                stage.Deactivate(userId);

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Stage config {StageId} ({Code}) upserted (created) for tenant {TenantId} product {Product}",
                stage.Id, code, tenantId, productCode);

            return TaskStageDto.From(stage);
        }
        else
        {
            existing.Update(request.Name, request.DisplayOrder, request.IsActive, userId, request.ProductSettingsJson);
            await _stages.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Stage config {StageId} ({Code}) upserted (updated) for tenant {TenantId} product {Product}",
                existing.Id, code, tenantId, productCode);

            return TaskStageDto.From(existing);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskStageDto>> ListAsync(
        Guid tenantId, string? sourceProductCode = null, CancellationToken ct = default)
    {
        var stages = await _stages.GetByTenantAsync(tenantId, sourceProductCode, activeOnly: true, ct);
        return stages.Select(TaskStageDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskStageDto?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var stage = await _stages.GetByIdAsync(tenantId, id, ct);
        return stage is null ? null : TaskStageDto.From(stage);
    }
}
