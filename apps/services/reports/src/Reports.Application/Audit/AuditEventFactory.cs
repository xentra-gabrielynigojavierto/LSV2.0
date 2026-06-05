using System.Text.Json;
using Reports.Contracts.Audit;
using Reports.Contracts.Context;

namespace Reports.Application.Audit;

public static class AuditEventFactory
{
    private const string SourceService = "reports-service";

    public static AuditEventDto TemplateCreated(string tenantId, string userId, Guid templateId, string templateCode, string? productCode, RequestContext? ctx = null)
        => Build("template.created", tenantId, userId, "ReportTemplate", templateId.ToString(), $"Template '{templateCode}' created", productCode, ctx,
            metadata: new { templateCode });

    public static AuditEventDto TemplateUpdated(string tenantId, string userId, Guid templateId, string templateCode, string? productCode, RequestContext? ctx = null)
        => Build("template.updated", tenantId, userId, "ReportTemplate", templateId.ToString(), $"Template '{templateCode}' updated", productCode, ctx,
            metadata: new { templateCode });

    public static AuditEventDto VersionCreated(string tenantId, string userId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("version.created", tenantId, userId, "ReportTemplateVersion", templateId.ToString(), $"Version {versionNumber} created for template '{templateCode}'", productCode, ctx,
            metadata: new { templateCode, versionNumber });

    public static AuditEventDto VersionPublished(string tenantId, string userId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("version.published", tenantId, userId, "ReportTemplateVersion", templateId.ToString(), $"Version {versionNumber} published for template '{templateCode}'", productCode, ctx,
            metadata: new { templateCode, versionNumber });

    public static AuditEventDto AssignmentCreated(string tenantId, string userId, Guid assignmentId, Guid templateId, string scope, RequestContext? ctx = null)
        => Build("template.assignment.created", tenantId, userId, "ReportTemplateAssignment", assignmentId.ToString(), $"Assignment created for template '{templateId}' scope '{scope}'", null, ctx,
            metadata: new { templateId, scope });

    public static AuditEventDto AssignmentUpdated(string tenantId, string userId, Guid assignmentId, Guid templateId, RequestContext? ctx = null)
        => Build("template.assignment.updated", tenantId, userId, "ReportTemplateAssignment", assignmentId.ToString(), $"Assignment updated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto TenantCatalogResolved(string tenantId, string? productCode, int count, RequestContext? ctx = null)
        => Build("tenant.catalog.resolved", tenantId, "system", "TenantCatalog", tenantId, $"Tenant catalog resolved: {count} templates", productCode, ctx,
            metadata: new { count });

    public static AuditEventDto OverrideCreated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.created", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override created for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideReactivated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.reactivated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override reactivated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideUpdated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.updated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override updated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideDeactivated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.deactivated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override deactivated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto EffectiveReportResolved(string tenantId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.effective.report.resolved", tenantId, "system", "EffectiveReport", templateId.ToString(), $"Effective report resolved for template '{templateId}'", null, ctx);

    public static AuditEventDto ExecutionStarted(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.started", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution started for template '{templateCode}' v{versionNumber}", productCode, ctx,
            metadata: new { templateId, templateCode, versionNumber });

    public static AuditEventDto ExecutionCompleted(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, int rowCount, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.completed", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution completed for template '{templateCode}' — {rowCount} rows", productCode, ctx,
            outcome: "Success", metadata: new { templateId, templateCode, rowCount });

    public static AuditEventDto ExecutionFailed(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.failed", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution failed for template '{templateCode}': {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { templateId, templateCode, reason });

    public static AuditEventDto ExportStarted(string tenantId, string userId, Guid exportId, Guid templateId, string format, string? productCode, RequestContext? ctx = null)
        => Build("report.export.started", tenantId, userId, "ReportExport", exportId.ToString(), $"Export started: template '{templateId}' format {format}", productCode, ctx,
            metadata: new { templateId, format });

    public static AuditEventDto ExportCompleted(string tenantId, string userId, Guid exportId, Guid templateId, string format, int rowCount, long fileSize, string? productCode, RequestContext? ctx = null)
        => Build("report.export.completed", tenantId, userId, "ReportExport", exportId.ToString(), $"Export completed: {rowCount} rows, {fileSize} bytes, format {format}", productCode, ctx,
            outcome: "Success", metadata: new { templateId, format, rowCount, fileSize });

    public static AuditEventDto ExportFailed(string tenantId, string userId, Guid exportId, Guid templateId, string format, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.export.failed", tenantId, userId, "ReportExport", exportId.ToString(), $"Export failed: {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { templateId, format, reason });

    public static AuditEventDto ScheduleCreated(string tenantId, string userId, Guid scheduleId, Guid templateId, string scheduleName, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.created", tenantId, userId, "ReportSchedule", scheduleId.ToString(), $"Schedule '{scheduleName}' created for template '{templateId}'", productCode, ctx,
            metadata: new { templateId, scheduleName });

    public static AuditEventDto ScheduleUpdated(string tenantId, string userId, Guid scheduleId, Guid templateId, string scheduleName, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.updated", tenantId, userId, "ReportSchedule", scheduleId.ToString(), $"Schedule '{scheduleName}' updated", productCode, ctx,
            metadata: new { templateId, scheduleName });

    public static AuditEventDto ScheduleDeactivated(string tenantId, string userId, Guid scheduleId, Guid templateId, string scheduleName, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.deactivated", tenantId, userId, "ReportSchedule", scheduleId.ToString(), $"Schedule '{scheduleName}' deactivated", productCode, ctx,
            metadata: new { templateId, scheduleName });

    public static AuditEventDto ScheduleRunStarted(string tenantId, string userId, Guid runId, Guid scheduleId, string scheduleName, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.run.started", tenantId, userId, "ReportScheduleRun", runId.ToString(), $"Schedule run started for '{scheduleName}'", productCode, ctx,
            metadata: new { scheduleId, scheduleName });

    public static AuditEventDto ScheduleRunCompleted(string tenantId, string userId, Guid runId, Guid scheduleId, string scheduleName, string fileName, long fileSize, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.run.completed", tenantId, userId, "ReportScheduleRun", runId.ToString(), $"Schedule run completed for '{scheduleName}' — {fileName} ({fileSize} bytes)", productCode, ctx,
            outcome: "Success", metadata: new { scheduleId, scheduleName, fileName, fileSize });

    public static AuditEventDto ScheduleRunFailed(string tenantId, string userId, Guid runId, Guid scheduleId, string scheduleName, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.schedule.run.failed", tenantId, userId, "ReportScheduleRun", runId.ToString(), $"Schedule run failed for '{scheduleName}': {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { scheduleId, scheduleName, reason });

    public static AuditEventDto ScheduleDeliveryCompleted(string tenantId, string userId, Guid runId, Guid scheduleId, string deliveryMethod, string? productCode, RequestContext? ctx = null,
        string? externalReferenceId = null, long? durationMs = null, string? storageKey = null)
        => Build("report.schedule.delivery.completed", tenantId, userId, "ReportScheduleRun", runId.ToString(), $"Delivery completed via {deliveryMethod}", productCode, ctx,
            outcome: "Success", metadata: new { scheduleId, deliveryMethod, externalReferenceId, durationMs, storageKey });

    public static AuditEventDto ScheduleDeliveryFailed(string tenantId, string userId, Guid runId, Guid scheduleId, string deliveryMethod, string reason, string? productCode, RequestContext? ctx = null,
        string? externalReferenceId = null, long? durationMs = null)
        => Build("report.schedule.delivery.failed", tenantId, userId, "ReportScheduleRun", runId.ToString(), $"Delivery failed via {deliveryMethod}: {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { scheduleId, deliveryMethod, reason, externalReferenceId, durationMs });

    public static AuditEventDto FileStored(string tenantId, string userId, Guid exportId, string storageKey, string provider, long sizeBytes, string? productCode, RequestContext? ctx = null)
        => Build("report.file.stored", tenantId, userId, "ReportExport", exportId.ToString(), $"File stored: key={storageKey} provider={provider}", productCode, ctx,
            outcome: "Success", metadata: new { storageKey, provider, sizeBytes });

    public static AuditEventDto FileStoreFailed(string tenantId, string userId, Guid exportId, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.file.store.failed", tenantId, userId, "ReportExport", exportId.ToString(), $"File store failed: {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { reason });

    public static AuditEventDto ViewCreated(string tenantId, string userId, Guid viewId, Guid templateId, string viewName, RequestContext? ctx = null)
        => Build("tenant.view.created", tenantId, userId, "TenantReportView", viewId.ToString(), $"View '{viewName}' created for template '{templateId}'", null, ctx,
            metadata: new { templateId, viewName });

    public static AuditEventDto ViewUpdated(string tenantId, string userId, Guid viewId, Guid templateId, string viewName, RequestContext? ctx = null)
        => Build("tenant.view.updated", tenantId, userId, "TenantReportView", viewId.ToString(), $"View '{viewName}' updated for template '{templateId}'", null, ctx,
            metadata: new { templateId, viewName });

    public static AuditEventDto ViewDeleted(string tenantId, string userId, Guid viewId, Guid templateId, string viewName, RequestContext? ctx = null)
        => Build("tenant.view.deleted", tenantId, userId, "TenantReportView", viewId.ToString(), $"View '{viewName}' deleted from template '{templateId}'", null, ctx,
            metadata: new { templateId, viewName });

    private static AuditEventDto Build(
        string eventType,
        string tenantId,
        string actorUserId,
        string entityType,
        string entityId,
        string description,
        string? productCode,
        RequestContext? ctx,
        string outcome = "Success",
        object? metadata = null)
    {
        return new AuditEventDto
        {
            EventType = eventType,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            ProductCode = productCode,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            CorrelationId = ctx?.CorrelationId,
            RequestId = ctx?.RequestId,
            Outcome = outcome,
            Action = eventType,
            Description = description,
            MetadataJson = metadata is not null ? JsonSerializer.Serialize(metadata) : null
        };
    }
}
