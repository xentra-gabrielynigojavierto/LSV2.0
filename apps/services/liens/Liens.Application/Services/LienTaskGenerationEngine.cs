using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienTaskGenerationEngine : ILienTaskGenerationEngine
{
    private readonly ILienTaskGenerationRuleRepository _ruleRepo;
    private readonly ILienTaskTemplateService          _templateService;
    private readonly ILienTaskRepository               _taskRepo;
    private readonly ILienTaskService                  _taskService;
    private readonly ILiensTaskServiceClient           _taskServiceClient;
    private readonly IAuditPublisher                   _audit;
    private readonly ILogger<LienTaskGenerationEngine> _logger;

    public LienTaskGenerationEngine(
        ILienTaskGenerationRuleRepository ruleRepo,
        ILienTaskTemplateService templateService,
        ILienTaskRepository taskRepo,
        ILienTaskService taskService,
        ILiensTaskServiceClient taskServiceClient,
        IAuditPublisher audit,
        ILogger<LienTaskGenerationEngine> logger)
    {
        _ruleRepo          = ruleRepo;
        _templateService   = templateService;
        _taskRepo          = taskRepo;
        _taskService       = taskService;
        _taskServiceClient = taskServiceClient;
        _audit             = audit;
        _logger            = logger;
    }

    public async Task<TaskGenerationResult> TriggerAsync(
        TaskGenerationContext context, CancellationToken ct = default)
    {
        if (!TaskGenerationEventType.All.Contains(context.EventType))
        {
            _logger.LogWarning("TaskGenerationEngine: unknown eventType '{EventType}'.", context.EventType);
            return new TaskGenerationResult(0, 0);
        }

        var rules = await _ruleRepo.GetActiveByTenantAndEventAsync(context.TenantId, context.EventType, ct);

        // TASK-MIG-06 — generation rules are Liens-DB-only (not yet migrated to Task service)
        _logger.LogDebug(
            "generation_rule_source=liens_db TenantId={TenantId} EventType={EventType} RuleCount={RuleCount}",
            context.TenantId, context.EventType, rules.Count);

        if (rules.Count == 0)
            return new TaskGenerationResult(0, 0);

        int generated = 0;
        int skipped   = 0;

        var actorUserId = context.ActorUserId ?? Guid.Empty;

        foreach (var rule in rules)
        {
            try
            {
                var outcome = await ProcessRuleAsync(rule, context, actorUserId, ct);
                if (outcome) generated++;
                else         skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TaskGenerationEngine: error processing rule {RuleId} for tenant {TenantId}.",
                    rule.Id, context.TenantId);
                skipped++;
            }
        }

        return new TaskGenerationResult(generated, skipped);
    }

    private async Task<bool> ProcessRuleAsync(
        LienTaskGenerationRule rule,
        TaskGenerationContext context,
        Guid actorUserId,
        CancellationToken ct)
    {
        // 1. Stage filter
        // TASK-MIG-06: rule.ApplicableWorkflowStageId is a Liens-DB-owned field (rules not yet migrated to Task).
        // Stage GUIDs are safe to compare directly — MIG-03 preserved them verbatim across both systems.
        if (rule.ApplicableWorkflowStageId.HasValue
            && rule.ApplicableWorkflowStageId.Value != context.WorkflowStageId)
        {
            _logger.LogDebug(
                "generation_stage_filter_source=liens_db Rule {RuleId}: stage mismatch (rule requires {Required}, context has {Actual}). Skipping.",
                rule.Id, rule.ApplicableWorkflowStageId, context.WorkflowStageId);
            return false;
        }

        _logger.LogDebug(
            "generation_stage_filter_source=liens_db Rule {RuleId}: stage filter passed (required={Required} context={Actual}).",
            rule.Id, rule.ApplicableWorkflowStageId?.ToString() ?? "any", context.WorkflowStageId?.ToString() ?? "none");

        // 2. Template check — TASK-MIG-02: dual-read (Task service first, Liens DB fallback)
        var template = await _templateService.GetForGenerationAsync(context.TenantId, rule.TaskTemplateId, ct);
        if (template is null || !template.IsActive)
        {
            _logger.LogWarning(
                "Rule {RuleId}: template {TemplateId} not found or inactive. Skipping.",
                rule.Id, rule.TaskTemplateId);
            _audit.Publish(
                eventType:   "liens.task.auto_generation_skipped",
                action:      "auto_generate_skipped",
                description: $"Skipped: template {rule.TaskTemplateId} not found or inactive for rule '{rule.Name}'",
                tenantId:    context.TenantId,
                actorUserId: actorUserId,
                entityType:  "LienTaskGenerationRule",
                entityId:    rule.Id.ToString());
            return false;
        }

        // 3. Duplicate prevention — TASK-B04-01 / TASK-MIG-06: uses Task service HTTP client exclusively.
        //    The Task service stores generationRuleId and generatingTemplateId on every auto-generated task,
        //    enabling open-task queries without touching liens_Tasks directly.
        var dupMode = rule.DuplicatePreventionMode;
        if (dupMode == DuplicatePreventionMode.SameRuleSameEntityOpenTask)
        {
            _logger.LogDebug(
                "generation_duplicate_check_source=task_service Rule {RuleId}: checking SAME_RULE dup (caseId={CaseId} lienId={LienId}).",
                rule.Id, context.CaseId, context.LienId);

            var hasDup = await _taskServiceClient.HasOpenTaskForRuleAsync(
                context.TenantId, rule.Id, context.CaseId, context.LienId, ct);
            if (hasDup)
            {
                _logger.LogInformation(
                    "generation_duplicate_check_source=task_service Rule {RuleId}: duplicate found (SAME_RULE). Skipping.", rule.Id);
                _audit.Publish(
                    eventType:   "liens.task.auto_generation_skipped",
                    action:      "auto_generate_skipped",
                    description: $"Skipped: open task already exists for rule '{rule.Name}' (SAME_RULE_SAME_ENTITY_OPEN_TASK)",
                    tenantId:    context.TenantId,
                    actorUserId: actorUserId,
                    entityType:  "LienTaskGenerationRule",
                    entityId:    rule.Id.ToString());
                return false;
            }
        }
        else if (dupMode == DuplicatePreventionMode.SameTemplateSameEntityOpenTask)
        {
            _logger.LogDebug(
                "generation_duplicate_check_source=task_service Rule {RuleId}: checking SAME_TEMPLATE dup (templateId={TemplateId} caseId={CaseId} lienId={LienId}).",
                rule.Id, rule.TaskTemplateId, context.CaseId, context.LienId);

            var hasDup = await _taskServiceClient.HasOpenTaskForTemplateAsync(
                context.TenantId, rule.TaskTemplateId, context.CaseId, context.LienId, ct);
            if (hasDup)
            {
                _logger.LogInformation(
                    "generation_duplicate_check_source=task_service Rule {RuleId}: duplicate found (SAME_TEMPLATE). Skipping.", rule.Id);
                _audit.Publish(
                    eventType:   "liens.task.auto_generation_skipped",
                    action:      "auto_generate_skipped",
                    description: $"Skipped: open task already exists for template '{template.Name}' (SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK)",
                    tenantId:    context.TenantId,
                    actorUserId: actorUserId,
                    entityType:  "LienTaskGenerationRule",
                    entityId:    rule.Id.ToString());
                return false;
            }
        }
        else
        {
            // DuplicatePreventionMode.None — TASK-MIG-06: logged for observability
            _logger.LogDebug(
                "generation_duplicate_check_source=none Rule {RuleId}: dup prevention mode is NONE; skipping dup check.",
                rule.Id);
        }

        // 4. Build task request
        var assignedUserId = ResolveAssignee(rule.AssignmentMode, template.DefaultRoleId, actorUserId, context);
        var dueDate        = ResolveDueDate(rule.DueDateMode, template.DefaultDueOffsetDays, rule.DueDateOffsetDays);

        var lienIds = context.LienId.HasValue
            ? new List<Guid> { context.LienId.Value }
            : new List<Guid>();

        var createRequest = new CreateTaskRequest
        {
            Title                 = template.DefaultTitle,
            Description           = template.DefaultDescription,
            Priority              = template.DefaultPriority,
            AssignedUserId        = assignedUserId,
            CaseId                = context.CaseId,
            LienIds               = lienIds,
            WorkflowStageId       = context.WorkflowStageId ?? template.ApplicableWorkflowStageId,
            DueDate               = dueDate,
            SourceType            = TaskSourceType.SystemGenerated,
            GenerationRuleId      = rule.Id,
            GeneratingTemplateId  = rule.TaskTemplateId,
        };

        // actorUserId guard — system-generated tasks use the event actor if available,
        // else we cannot use Guid.Empty as createdByUserId. Fall back to a deterministic
        // placeholder (we never call with Guid.Empty as that throws in LienTask.Create).
        if (actorUserId == Guid.Empty)
        {
            _logger.LogWarning("Rule {RuleId}: no actor userId available; task generation requires a valid userId. Skipping.", rule.Id);
            return false;
        }

        // 5. Create task via normal pipeline
        var taskResponse = await _taskService.CreateAsync(context.TenantId, actorUserId, createRequest, ct);

        // 6. Save traceability metadata
        var metadata = LienGeneratedTaskMetadata.Create(
            taskId:            taskResponse.Id,
            tenantId:          context.TenantId,
            generationRuleId:  rule.Id,
            taskTemplateId:    rule.TaskTemplateId,
            triggerEventType:  context.EventType,
            triggerEntityType: context.EntityType,
            triggerEntityId:   context.EntityId.ToString());

        await _taskRepo.AddGeneratedMetadataAsync(metadata, ct);

        // 7. Audit success
        _audit.Publish(
            eventType:   "liens.task.auto_generated",
            action:      "auto_generate",
            description: $"Task '{taskResponse.Title}' auto-generated by rule '{rule.Name}' on event {context.EventType}",
            tenantId:    context.TenantId,
            actorUserId: actorUserId,
            entityType:  "LienTask",
            entityId:    taskResponse.Id.ToString(),
            metadata:    $"{{\"ruleId\":\"{rule.Id}\",\"templateId\":\"{rule.TaskTemplateId}\",\"eventType\":\"{context.EventType}\",\"triggerEntityId\":\"{context.EntityId}\"}}");

        _logger.LogInformation(
            "TaskGenerationEngine: task {TaskId} generated for rule {RuleId} event {EventType}.",
            taskResponse.Id, rule.Id, context.EventType);

        return true;
    }

    private static Guid? ResolveAssignee(
        string assignmentMode,
        string? templateRoleId,
        Guid actorUserId,
        TaskGenerationContext context)
    {
        return assignmentMode switch
        {
            AssignmentMode.LeaveUnassigned  => null,
            AssignmentMode.AssignEventActor => actorUserId == Guid.Empty ? null : actorUserId,
            AssignmentMode.AssignByRole     => null, // Deferred: no role-to-user resolution available
            _                               => null, // UseTemplateDefault: template has no assignedUserId directly
        };
    }

    private static DateTime? ResolveDueDate(
        string dueDateMode,
        int? templateOffsetDays,
        int? ruleOffsetDays)
    {
        return dueDateMode switch
        {
            DueDateMode.NoDueDate         => null,
            DueDateMode.OverrideOffsetDays => ruleOffsetDays.HasValue
                ? DateTime.UtcNow.AddDays(ruleOffsetDays.Value)
                : null,
            _ => templateOffsetDays.HasValue
                ? DateTime.UtcNow.AddDays(templateOffsetDays.Value)
                : null,
        };
    }
}
