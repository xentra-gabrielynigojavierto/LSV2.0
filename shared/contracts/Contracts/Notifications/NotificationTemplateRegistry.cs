using System.Collections.Concurrent;

namespace Contracts.Notifications;

/// <summary>
/// In-process, read-mostly registry of well-known notification templates
/// (E12.1). Seeded at startup with deterministic platform defaults; the
/// notifications service may add tenant overrides on top at render time
/// (handled outside this class).
///
/// <para>
/// Phrasing in the seeded defaults is intentionally generic and
/// product-neutral — per-product wording lives in tenant template
/// overrides, not in the platform registry.
/// </para>
/// </summary>
public sealed class NotificationTemplateRegistry
{
    private readonly ConcurrentDictionary<string, NotificationTemplate> _byKey =
        new(StringComparer.Ordinal);

    public NotificationTemplateRegistry(IEnumerable<NotificationTemplate>? seed = null)
    {
        foreach (var t in seed ?? PlatformDefaults())
        {
            _byKey[t.Key] = t;
        }
    }

    public NotificationTemplate? Get(string key) =>
        _byKey.TryGetValue(key, out var t) ? t : null;

    public bool TryGet(string key, out NotificationTemplate template)
    {
        if (_byKey.TryGetValue(key, out var found))
        {
            template = found;
            return true;
        }
        template = null!;
        return false;
    }

    public IReadOnlyCollection<NotificationTemplate> All() => _byKey.Values.ToList();

    /// <summary>
    /// Add or replace a template definition. Used by the notifications
    /// service when registering tenant-scoped overrides at startup.
    /// </summary>
    public void Upsert(NotificationTemplate template) => _byKey[template.Key] = template;

    // ------------------------------------------------------------------
    // Platform defaults — generic, channel-neutral phrasing.
    // ------------------------------------------------------------------

    public static IEnumerable<NotificationTemplate> PlatformDefaults()
    {
        // Workflow lifecycle
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowCompleted,
            Name            = "Workflow completed",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow completed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} has completed.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Workflow,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true,
                    Description = "Identifier of the workflow instance that completed." },
                new NotificationTokenDefinition { Name = "productKey",         Required = false,
                    Description = "Product key the workflow belongs to." },
            },
        };

        // Workflow SLA transitions
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaDueSoon,
            Name            = "Workflow due soon",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow due soon",
            BodyTemplate    = "Workflow {{workflowInstanceId}} is due at {{dueAt}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "dueAt",              Required = true,
                    Description = "ISO-8601 UTC due timestamp." },
                new NotificationTokenDefinition { Name = "productKey",         Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaOverdue,
            Name            = "Workflow overdue",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow overdue",
            BodyTemplate    = "Workflow {{workflowInstanceId}} is overdue (due {{dueAt}}).",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "dueAt",              Required = true },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowSlaEscalated,
            Name            = "Workflow escalated",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Workflow escalated",
            BodyTemplate    = "Workflow {{workflowInstanceId}} has been overdue for {{overdueMinutes}} minute(s); escalation level {{escalationLevel}}.",
            Severity        = NotificationSeverity.Critical,
            Category        = NotificationCategory.Sla,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "overdueMinutes",     Required = true },
                new NotificationTokenDefinition { Name = "escalationLevel",    Required = true },
            },
        };

        // Workflow admin actions
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminRetry,
            Name            = "Workflow re-armed by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow re-armed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was re-armed by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminForceComplete,
            Name            = "Workflow force-completed by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow force-completed",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was force-completed by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.WorkflowAdminCancel,
            Name            = "Workflow cancelled by admin",
            Channels        = new[] { NotificationChannels.InApp },
            SubjectTemplate = "Workflow cancelled",
            BodyTemplate    = "Workflow {{workflowInstanceId}} was cancelled by an operator: {{reason}}.",
            Severity        = NotificationSeverity.Warning,
            Category        = NotificationCategory.Admin,
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "workflowInstanceId", Required = true },
                new NotificationTokenDefinition { Name = "reason",             Required = false },
            },
        };

        // Task lifecycle (reserved — actual emission lands in later phases).
        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.TaskAssigned,
            Name            = "Task assigned",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Task assigned: {{taskTitle}}",
            BodyTemplate    = "Task '{{taskTitle}}' has been assigned to you.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Task,
            Enabled         = false, // wiring deferred to E12.x
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "taskId",    Required = true },
                new NotificationTokenDefinition { Name = "taskTitle", Required = true },
            },
        };

        yield return new NotificationTemplate
        {
            Key             = NotificationTemplateKeys.TaskCompleted,
            Name            = "Task completed",
            Channels        = new[] { NotificationChannels.Email, NotificationChannels.InApp },
            SubjectTemplate = "Task completed: {{taskTitle}}",
            BodyTemplate    = "Task '{{taskTitle}}' has been completed.",
            Severity        = NotificationSeverity.Info,
            Category        = NotificationCategory.Task,
            Enabled         = false, // wiring deferred to E12.x
            Tokens = new[]
            {
                new NotificationTokenDefinition { Name = "taskId",    Required = true },
                new NotificationTokenDefinition { Name = "taskTitle", Required = true },
            },
        };
    }
}
