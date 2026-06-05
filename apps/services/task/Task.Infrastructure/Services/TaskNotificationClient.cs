using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts.Notifications;
using Task.Application.Interfaces;
using Task.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Task.Infrastructure.Services;

/// <summary>
/// Sends task lifecycle and reminder notifications via the canonical Notifications
/// service endpoint (<c>POST /v1/notifications</c>) using <see cref="NotificationEnvelope"/>.
///
/// Failures are logged at Warning level and do NOT propagate — notification delivery
/// must never break task operation flow.
/// </summary>
public sealed class TaskNotificationClient : ITaskNotificationClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ProductKey    = "tasks";
    private const string SourceSystem  = "task-service";
    private const string TaskAssigned  = "task.assigned";
    private const string TaskReassigned = "task.reassigned";
    private const string ReminderDueSoon = "task.reminder.due_soon";
    private const string ReminderOverdue = "task.reminder.overdue";

    private readonly IHttpClientFactory                  _httpClientFactory;
    private readonly TaskNotificationsServiceOptions     _options;
    private readonly ILogger<TaskNotificationClient>     _logger;

    public TaskNotificationClient(
        IHttpClientFactory                        httpClientFactory,
        IOptions<TaskNotificationsServiceOptions> options,
        ILogger<TaskNotificationClient>           logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
    }

    public async System.Threading.Tasks.Task NotifyAssignedAsync(
        Guid      tenantId,
        Guid      taskId,
        string    taskTitle,
        Guid      assignedUserId,
        string?   sourceProductCode,
        CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(
            TaskAssigned, tenantId, taskId, taskTitle, assignedUserId, sourceProductCode,
            NotificationCategory.Task, NotificationSeverity.Info,
            new Dictionary<string, string?>
            {
                ["taskId"]    = taskId.ToString(),
                ["taskTitle"] = taskTitle,
            });
        await SubmitAsync(envelope, tenantId, "NotifyAssigned", ct);
    }

    public async System.Threading.Tasks.Task NotifyReassignedAsync(
        Guid      tenantId,
        Guid      taskId,
        string    taskTitle,
        Guid      assignedUserId,
        string?   sourceProductCode,
        CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(
            TaskReassigned, tenantId, taskId, taskTitle, assignedUserId, sourceProductCode,
            NotificationCategory.Task, NotificationSeverity.Info,
            new Dictionary<string, string?>
            {
                ["taskId"]    = taskId.ToString(),
                ["taskTitle"] = taskTitle,
            });
        await SubmitAsync(envelope, tenantId, "NotifyReassigned", ct);
    }

    public async System.Threading.Tasks.Task NotifyReminderAsync(
        Guid      tenantId,
        Guid      taskId,
        string    taskTitle,
        Guid      assignedUserId,
        string    reminderType,
        DateTime? dueAt,
        string?   sourceProductCode,
        CancellationToken ct = default)
    {
        var templateKey = reminderType == ReminderType.Overdue ? ReminderOverdue : ReminderDueSoon;
        var severity    = reminderType == ReminderType.Overdue
            ? NotificationSeverity.Critical
            : NotificationSeverity.Warning;

        var envelope = BuildEnvelope(
            templateKey, tenantId, taskId, taskTitle, assignedUserId, sourceProductCode,
            NotificationCategory.Sla, severity,
            new Dictionary<string, string?>
            {
                ["taskId"]       = taskId.ToString(),
                ["taskTitle"]    = taskTitle,
                ["dueAt"]        = dueAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["reminderType"] = reminderType,
            });
        await SubmitAsync(envelope, tenantId, $"NotifyReminder({reminderType})", ct);
    }

    private static NotificationEnvelope BuildEnvelope(
        string                            templateKey,
        Guid                              tenantId,
        Guid                              taskId,
        string                            taskTitle,
        Guid                              assignedUserId,
        string?                           sourceProductCode,
        string                            category,
        string                            severity,
        IReadOnlyDictionary<string, string?> bodyVariables) => new()
    {
        TemplateKey     = templateKey,
        TenantId        = tenantId.ToString(),
        ProductKey      = string.IsNullOrWhiteSpace(sourceProductCode) ? ProductKey : sourceProductCode.ToLowerInvariant(),
        EntityType      = "Task",
        EntityId        = taskId.ToString(),
        Recipient       = NotificationRecipient.ForUser(assignedUserId.ToString(), tenantId.ToString()),
        BodyVariables   = bodyVariables,
        Category        = category,
        Severity        = severity,
        CorrelationId   = taskId.ToString(),
    };

    private async System.Threading.Tasks.Task SubmitAsync(
        NotificationEnvelope envelope,
        Guid                 tenantId,
        string               logTag,
        CancellationToken    ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning(
                "[TaskNotif/{Tag}] NotificationsService:BaseUrl is not configured — notification skipped.",
                logTag);
            return;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("TaskNotificationsService");
            client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 5));

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/notifications");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Content = JsonContent.Create(envelope, options: JsonOpts);

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                // TASK-B05 (TASK-016) — include correlationId (=taskId) for cross-service tracing
                _logger.LogWarning(
                    "[TaskNotif/{Tag}] Notifications service returned {StatusCode} " +
                    "(correlationId={CorrelationId}). Body: {Body}",
                    logTag, (int)response.StatusCode, envelope.CorrelationId,
                    body.Length > 500 ? body[..500] : body);
            }
        }
        catch (Exception ex)
        {
            // TASK-B05 (TASK-016) — include correlationId (=taskId) for cross-service tracing
            _logger.LogWarning(ex,
                "[TaskNotif/{Tag}] Failed to submit notification to Notifications service " +
                "(tenant={TenantId}, correlationId={CorrelationId}).",
                logTag, tenantId, envelope.CorrelationId);
        }
    }
}
