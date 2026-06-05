using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LienGeneratedTaskMetadata
{
    public Guid   TaskId             { get; private set; }
    public Guid   TenantId           { get; private set; }
    public Guid   GenerationRuleId   { get; private set; }
    public Guid   TaskTemplateId     { get; private set; }
    public string TriggerEventType   { get; private set; } = string.Empty;
    public string TriggerEntityType  { get; private set; } = string.Empty;
    public string TriggerEntityId    { get; private set; } = string.Empty;
    public string SourceType         { get; private set; } = TaskSourceType.SystemGenerated;
    public DateTime GeneratedAt      { get; private set; }

    private LienGeneratedTaskMetadata() { }

    public static LienGeneratedTaskMetadata Create(
        Guid   taskId,
        Guid   tenantId,
        Guid   generationRuleId,
        Guid   taskTemplateId,
        string triggerEventType,
        string triggerEntityType,
        string triggerEntityId)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId required.", nameof(taskId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerEventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerEntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerEntityId);

        return new LienGeneratedTaskMetadata
        {
            TaskId            = taskId,
            TenantId          = tenantId,
            GenerationRuleId  = generationRuleId,
            TaskTemplateId    = taskTemplateId,
            TriggerEventType  = triggerEventType,
            TriggerEntityType = triggerEntityType,
            TriggerEntityId   = triggerEntityId,
            SourceType        = TaskSourceType.SystemGenerated,
            GeneratedAt       = DateTime.UtcNow,
        };
    }
}
