using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskTemplateService : ITaskTemplateService
{
    private readonly ITaskTemplateRepository    _templates;
    private readonly ITaskRepository            _tasks;
    private readonly ITaskHistoryRepository     _history;
    private readonly ITaskGovernanceService     _governance;
    private readonly ITaskReminderService       _reminders;
    private readonly IUnitOfWork                _uow;
    private readonly ILogger<TaskTemplateService> _logger;

    public TaskTemplateService(
        ITaskTemplateRepository    templates,
        ITaskRepository            tasks,
        ITaskHistoryRepository     history,
        ITaskGovernanceService     governance,
        ITaskReminderService       reminders,
        IUnitOfWork                uow,
        ILogger<TaskTemplateService> logger)
    {
        _templates  = templates;
        _tasks      = tasks;
        _history    = history;
        _governance = governance;
        _reminders  = reminders;
        _uow        = uow;
        _logger     = logger;
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto> CreateAsync(
        Guid tenantId, Guid userId, CreateTaskTemplateRequest req, CancellationToken ct = default)
    {
        // TASK-B05 (TASK-014) — validate product code against canonical registry
        var productCode = KnownProductCodes.ValidateOptional(req.SourceProductCode);

        var template = TaskTemplate.Create(
            tenantId, req.Code, req.Name, req.DefaultTitle, userId,
            productCode, req.Description, req.DefaultDescription,
            req.DefaultPriority, req.DefaultScope, req.DefaultDueInDays, req.DefaultStageId,
            req.ProductSettingsJson);

        await _templates.AddAsync(template, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Template {TemplateId} ({Code}) created for tenant {TenantId}", template.Id, template.Code, tenantId);

        return TaskTemplateDto.From(template);
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto> UpdateAsync(
        Guid tenantId, Guid id, Guid userId, UpdateTaskTemplateRequest req, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Template {id} not found.");

        template.Update(req.Name, req.DefaultTitle, userId, req.ExpectedVersion,
            req.Description, req.DefaultDescription, req.DefaultPriority,
            req.DefaultScope, req.DefaultDueInDays, req.DefaultStageId,
            req.ProductSettingsJson);

        await _uow.SaveChangesAsync(ct);
        return TaskTemplateDto.From(template);
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto> ActivateAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Template {id} not found.");

        template.Activate(userId);
        await _uow.SaveChangesAsync(ct);
        return TaskTemplateDto.From(template);
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto> DeactivateAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Template {id} not found.");

        template.Deactivate(userId);
        await _uow.SaveChangesAsync(ct);
        return TaskTemplateDto.From(template);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskTemplateDto>> ListAsync(
        Guid tenantId, string? sourceProductCode = null, CancellationToken ct = default)
    {
        var templates = await _templates.GetByTenantAsync(tenantId, sourceProductCode, activeOnly: true, ct);
        return templates.Select(TaskTemplateDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, id, ct);
        return template is null ? null : TaskTemplateDto.From(template);
    }

    public async System.Threading.Tasks.Task<TaskTemplateDto> UpsertFromSourceAsync(
        Guid tenantId, Guid userId, UpsertFromSourceTemplateRequest req, CancellationToken ct = default)
    {
        var productCode = KnownProductCodes.ValidateOptional(req.SourceProductCode)
            ?? throw new ArgumentException("SourceProductCode is required for UpsertFromSource.", nameof(req));

        var existing = await _templates.GetByIdAsync(tenantId, req.Id, ct);

        if (existing is null)
        {
            var created = TaskTemplate.Create(
                tenantId, req.Code, req.Name, req.DefaultTitle, userId,
                productCode, req.Description, req.DefaultDescription,
                req.DefaultPriority, req.DefaultScope, req.DefaultDueInDays, req.DefaultStageId,
                req.ProductSettingsJson, req.Id);

            if (!req.IsActive) created.Deactivate(userId);

            await _templates.AddAsync(created, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Template {TemplateId} ({Code}) created via UpsertFromSource for tenant {TenantId}",
                created.Id, created.Code, tenantId);

            return TaskTemplateDto.From(created);
        }

        existing.Update(
            req.Name, req.DefaultTitle, userId, existing.Version,
            req.Description, req.DefaultDescription,
            req.DefaultPriority, req.DefaultScope,
            req.DefaultDueInDays, req.DefaultStageId,
            req.ProductSettingsJson);

        if (req.IsActive && !existing.IsActive) existing.Activate(userId);
        else if (!req.IsActive && existing.IsActive) existing.Deactivate(userId);

        await _templates.UpdateAsync(existing, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Template {TemplateId} ({Code}) updated via UpsertFromSource for tenant {TenantId}",
            existing.Id, existing.Code, tenantId);

        return TaskTemplateDto.From(existing);
    }

    public async System.Threading.Tasks.Task<TaskDto> CreateTaskFromTemplateAsync(
        Guid tenantId, Guid userId, Guid templateId,
        CreateTaskFromTemplateRequest req, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, templateId, ct)
            ?? throw new NotFoundException($"Template {templateId} not found.");

        if (!template.IsActive)
            throw new InvalidOperationException($"Template '{template.Name}' is not active.");

        // Resolve governance
        var governance = await _governance.ResolveAsync(tenantId, template.SourceProductCode, ct);

        // Apply template defaults, allow caller overrides
        var title       = req.TitleOverride?.Trim() ?? template.DefaultTitle;
        var description = req.DescriptionOverride ?? template.DefaultDescription;
        var dueAt       = req.DueAtOverride
            ?? (template.DefaultDueInDays.HasValue
                ? DateTime.UtcNow.AddDays(template.DefaultDueInDays.Value)
                : (DateTime?)null);

        // Governance enforcement
        if (governance.RequireAssignee && req.AssignedUserId is null)
            throw new InvalidOperationException("Governance requires an assignee — provide AssignedUserId.");
        if (governance.RequireDueDate && dueAt is null)
            throw new InvalidOperationException("Governance requires a due date — provide DueAtOverride or set DefaultDueInDays on the template.");
        if (governance.RequireStage && template.DefaultStageId is null)
            throw new InvalidOperationException("Governance requires a stage — set DefaultStageId on the template.");

        var task = PlatformTask.Create(
            tenantId, title, userId,
            description, template.DefaultPriority, template.DefaultScope,
            req.AssignedUserId, template.SourceProductCode,
            req.SourceEntityType, req.SourceEntityId,
            dueAt, template.DefaultStageId);

        await _tasks.AddAsync(task, ct);

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, TaskActions.CreatedFromTemplate, userId,
                $"Created from template '{template.Name}' ({template.Code})"), ct);

        await _uow.SaveChangesAsync(ct);

        // Sync reminders if due date set
        if (dueAt.HasValue)
            await _reminders.SyncRemindersAsync(tenantId, task.Id, dueAt, ct);

        _logger.LogInformation(
            "Task {TaskId} created from template {TemplateId} by {UserId} in tenant {TenantId}",
            task.Id, templateId, userId, tenantId);

        return TaskDto.From(task);
    }
}
