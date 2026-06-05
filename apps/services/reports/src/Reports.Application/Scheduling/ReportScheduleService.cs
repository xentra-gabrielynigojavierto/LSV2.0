using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Execution;
using Reports.Application.Execution.DTOs;
using Reports.Application.Export;
using Reports.Application.Export.DTOs;
using Reports.Application.Scheduling.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Delivery;
using Reports.Contracts.Observability;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Scheduling;

public sealed class ReportScheduleService : IReportScheduleService
{
    private const int MaxSchedulesPerPoll = 10;

    private static readonly HashSet<string> SupportedFrequencies = new(StringComparer.OrdinalIgnoreCase) { "Daily", "Weekly", "Monthly" };
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase) { "CSV", "XLSX", "PDF" };
    private static readonly HashSet<string> SupportedDeliveryMethods = new(StringComparer.OrdinalIgnoreCase) { "OnScreen", "Email", "SFTP" };

    private readonly IReportScheduleRepository _scheduleRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly IReportExecutionService _executionService;
    private readonly IReportExportService _exportService;
    private readonly IEnumerable<IReportDeliveryAdapter> _deliveryAdapters;
    private readonly IAuditAdapter _audit;
    private readonly IReportsMetrics _metrics;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<ReportScheduleService> _log;

    public ReportScheduleService(
        IReportScheduleRepository scheduleRepo,
        ITemplateRepository templateRepo,
        ITemplateAssignmentRepository assignmentRepo,
        IReportExecutionService executionService,
        IReportExportService exportService,
        IEnumerable<IReportDeliveryAdapter> deliveryAdapters,
        IAuditAdapter audit,
        IReportsMetrics metrics,
        ICurrentTenantContext ctx,
        ILogger<ReportScheduleService> log)
    {
        _scheduleRepo = scheduleRepo;
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _executionService = executionService;
        _exportService = exportService;
        _deliveryAdapters = deliveryAdapters;
        _audit = audit;
        _metrics = metrics;
        _ctx = ctx;
        _log = log;
    }

    public async Task<ServiceResult<ReportScheduleResponse>> CreateScheduleAsync(
        CreateReportScheduleRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ReportScheduleResponse>.Forbidden("No authenticated user context.");

        var validation = ValidateCreateRequest(request);
        if (validation is not null)
            return ServiceResult<ReportScheduleResponse>.BadRequest(validation);

        var deliveryValidation = ValidateDeliveryConfig(request.DeliveryMethod, request.DeliveryConfigJson);
        if (deliveryValidation is not null)
            return ServiceResult<ReportScheduleResponse>.BadRequest(deliveryValidation);

        var template = await _templateRepo.GetByIdAsync(request.ReportTemplateId, ct);
        if (template is null)
            return ServiceResult<ReportScheduleResponse>.NotFound($"Template '{request.ReportTemplateId}' not found.");

        if (!template.IsActive)
            return ServiceResult<ReportScheduleResponse>.BadRequest($"Template '{request.ReportTemplateId}' is not active.");

        if (!string.Equals(template.ProductCode, request.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleResponse>.BadRequest(
                $"Product code mismatch: template product is '{template.ProductCode}', request specified '{request.ProductCode}'.");

        var isAssigned = await IsTenantAssignedAsync(request.ReportTemplateId, request.TenantId.Trim(), ct);
        if (!isAssigned)
            return ServiceResult<ReportScheduleResponse>.BadRequest(
                $"Template '{request.ReportTemplateId}' is not assigned to tenant '{request.TenantId}'.");

        var publishedVersion = await _templateRepo.GetPublishedVersionAsync(request.ReportTemplateId, ct);
        if (publishedVersion is null)
            return ServiceResult<ReportScheduleResponse>.NotFound(
                $"Template '{request.ReportTemplateId}' has no published version.");

        var nextRun = CalculateNextRun(request.FrequencyType, request.FrequencyConfigJson, request.Timezone, DateTimeOffset.UtcNow);

        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId.Trim(),
            ReportTemplateId = request.ReportTemplateId,
            ProductCode = request.ProductCode.Trim(),
            OrganizationType = request.OrganizationType.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            FrequencyType = request.FrequencyType.Trim(),
            FrequencyConfigJson = request.FrequencyConfigJson,
            Timezone = request.Timezone.Trim(),
            NextRunAtUtc = request.IsActive ? nextRun : null,
            UseOverride = request.UseOverride,
            ViewId = request.ViewId,
            ExportFormat = request.ExportFormat.Trim().ToUpperInvariant(),
            DeliveryMethod = request.DeliveryMethod.Trim(),
            DeliveryConfigJson = request.DeliveryConfigJson,
            ParametersJson = request.ParametersJson,
            RequiredFeatureCode = request.RequiredFeatureCode,
            MinimumTierCode = request.MinimumTierCode,
            CreatedByUserId = actorId
        };

        await _scheduleRepo.SaveAsync(schedule, ct);

        await TryAuditAsync(AuditEventFactory.ScheduleCreated(
            schedule.TenantId, actorId, schedule.Id,
            schedule.ReportTemplateId, schedule.Name, schedule.ProductCode));

        _log.LogInformation("Schedule created: {ScheduleId} tenant={TenantId} template={TemplateId} freq={Freq}",
            schedule.Id, schedule.TenantId, schedule.ReportTemplateId, schedule.FrequencyType);

        return ServiceResult<ReportScheduleResponse>.Created(MapToResponse(schedule));
    }

    public async Task<ServiceResult<ReportScheduleResponse>> UpdateScheduleAsync(
        Guid scheduleId, UpdateReportScheduleRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ReportScheduleResponse>.Forbidden("No authenticated user context.");

        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        if (!string.Equals(schedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        var validation = ValidateUpdateRequest(request);
        if (validation is not null)
            return ServiceResult<ReportScheduleResponse>.BadRequest(validation);

        var deliveryValidation = ValidateDeliveryConfig(request.DeliveryMethod, request.DeliveryConfigJson);
        if (deliveryValidation is not null)
            return ServiceResult<ReportScheduleResponse>.BadRequest(deliveryValidation);

        schedule.Name = request.Name.Trim();
        schedule.Description = request.Description?.Trim();
        schedule.IsActive = request.IsActive;
        schedule.FrequencyType = request.FrequencyType.Trim();
        schedule.FrequencyConfigJson = request.FrequencyConfigJson;
        schedule.Timezone = request.Timezone.Trim();
        schedule.UseOverride = request.UseOverride;
        schedule.ViewId = request.ViewId;
        schedule.ExportFormat = request.ExportFormat.Trim().ToUpperInvariant();
        schedule.DeliveryMethod = request.DeliveryMethod.Trim();
        schedule.DeliveryConfigJson = request.DeliveryConfigJson;
        schedule.ParametersJson = request.ParametersJson;
        schedule.RequiredFeatureCode = request.RequiredFeatureCode;
        schedule.MinimumTierCode = request.MinimumTierCode;
        schedule.UpdatedByUserId = actorId;
        schedule.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (schedule.IsActive)
            schedule.NextRunAtUtc = CalculateNextRun(schedule.FrequencyType, schedule.FrequencyConfigJson, schedule.Timezone, DateTimeOffset.UtcNow);
        else
            schedule.NextRunAtUtc = null;

        await _scheduleRepo.UpdateAsync(schedule, ct);

        await TryAuditAsync(AuditEventFactory.ScheduleUpdated(
            schedule.TenantId, actorId, schedule.Id,
            schedule.ReportTemplateId, schedule.Name, schedule.ProductCode));

        return ServiceResult<ReportScheduleResponse>.Ok(MapToResponse(schedule));
    }

    public async Task<ServiceResult<ReportScheduleResponse>> GetScheduleByIdAsync(Guid scheduleId, CancellationToken ct)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        if (!string.Equals(schedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        return ServiceResult<ReportScheduleResponse>.Ok(MapToResponse(schedule));
    }

    public async Task<ServiceResult<IReadOnlyList<ReportScheduleResponse>>> ListSchedulesAsync(
        string tenantId, string? productCode, int page, int pageSize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<IReadOnlyList<ReportScheduleResponse>>.BadRequest("TenantId is required.");

        var schedules = await _scheduleRepo.ListByTenantAsync(tenantId.Trim(), productCode?.Trim(), page, pageSize, ct);
        IReadOnlyList<ReportScheduleResponse> result = schedules.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<ReportScheduleResponse>>.Ok(result);
    }

    public async Task<ServiceResult<ReportScheduleResponse>> DeactivateScheduleAsync(
        Guid scheduleId, CancellationToken ct)
    {
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ReportScheduleResponse>.Forbidden("No authenticated user context.");

        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        if (!string.Equals(schedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        schedule.IsActive = false;
        schedule.NextRunAtUtc = null;
        schedule.UpdatedByUserId = actorId;
        schedule.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _scheduleRepo.UpdateAsync(schedule, ct);

        await TryAuditAsync(AuditEventFactory.ScheduleDeactivated(
            schedule.TenantId, actorId, schedule.Id, schedule.ReportTemplateId,
            schedule.Name, schedule.ProductCode));

        return ServiceResult<ReportScheduleResponse>.Ok(MapToResponse(schedule));
    }

    public async Task<ServiceResult<ReportScheduleRunResponse>> TriggerRunNowAsync(
        Guid scheduleId, CancellationToken ct)
    {
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ReportScheduleRunResponse>.Forbidden("No authenticated user context.");

        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return ServiceResult<ReportScheduleRunResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        if (!string.Equals(schedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleRunResponse>.NotFound($"Schedule '{scheduleId}' not found.");

        var run = await ExecuteScheduleRunAsync(schedule, DateTimeOffset.UtcNow, actorId, ct);
        return ServiceResult<ReportScheduleRunResponse>.Ok(MapRunToResponse(run));
    }

    public async Task<ServiceResult<IReadOnlyList<ReportScheduleRunResponse>>> ListRunsAsync(
        Guid scheduleId, int page, int pageSize, CancellationToken ct)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return ServiceResult<IReadOnlyList<ReportScheduleRunResponse>>.NotFound($"Schedule '{scheduleId}' not found.");

        if (!string.Equals(schedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<IReadOnlyList<ReportScheduleRunResponse>>.NotFound($"Schedule '{scheduleId}' not found.");

        var runs = await _scheduleRepo.ListRunsByScheduleAsync(scheduleId, page, pageSize, ct);
        IReadOnlyList<ReportScheduleRunResponse> result = runs.Select(MapRunToResponse).ToList();
        return ServiceResult<IReadOnlyList<ReportScheduleRunResponse>>.Ok(result);
    }

    public async Task<ServiceResult<ReportScheduleRunResponse>> GetRunByIdAsync(Guid runId, CancellationToken ct)
    {
        var run = await _scheduleRepo.GetRunByIdAsync(runId, ct);
        if (run is null)
            return ServiceResult<ReportScheduleRunResponse>.NotFound($"Run '{runId}' not found.");

        var owningSchedule = run.ReportSchedule
            ?? await _scheduleRepo.GetByIdAsync(run.ReportScheduleId, ct);

        if (owningSchedule is null ||
            !string.Equals(owningSchedule.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportScheduleRunResponse>.NotFound($"Run '{runId}' not found.");

        return ServiceResult<ReportScheduleRunResponse>.Ok(MapRunToResponse(run));
    }

    public async Task ProcessDueSchedulesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueSchedules = await _scheduleRepo.GetDueSchedulesAsync(now, MaxSchedulesPerPoll, ct);

        if (dueSchedules.Count == 0) return;

        _log.LogInformation("Processing {Count} due schedule(s)", dueSchedules.Count);

        foreach (var schedule in dueSchedules)
        {
            try
            {
                var scheduledFor = schedule.NextRunAtUtc ?? now;

                schedule.LastRunAtUtc = now;
                schedule.NextRunAtUtc = CalculateNextRun(
                    schedule.FrequencyType, schedule.FrequencyConfigJson, schedule.Timezone, now);
                await _scheduleRepo.UpdateAsync(schedule, ct);

                await ExecuteScheduleRunAsync(schedule, scheduledFor, schedule.CreatedByUserId, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to process schedule {ScheduleId}", schedule.Id);
            }
        }
    }

    private async Task<ReportScheduleRun> ExecuteScheduleRunAsync(
        ReportSchedule schedule, DateTimeOffset scheduledFor, string actorUserId, CancellationToken ct)
    {
        var run = new ReportScheduleRun
        {
            Id = Guid.NewGuid(),
            ReportScheduleId = schedule.Id,
            Status = "Pending",
            ScheduledForUtc = scheduledFor
        };
        await _scheduleRepo.SaveRunAsync(run, ct);

        await TryAuditAsync(AuditEventFactory.ScheduleRunStarted(
            schedule.TenantId, actorUserId, run.Id, schedule.Id,
            schedule.Name, schedule.ProductCode));

        run.Status = "Running";
        run.StartedAtUtc = DateTimeOffset.UtcNow;
        await _scheduleRepo.UpdateRunAsync(run, ct);

        ExportFormat format;
        if (!Enum.TryParse<ExportFormat>(schedule.ExportFormat, true, out format))
            format = ExportFormat.CSV;

        var exportRequest = new ExportReportRequest
        {
            TenantId = schedule.TenantId,
            TemplateId = schedule.ReportTemplateId,
            ProductCode = schedule.ProductCode,
            OrganizationType = schedule.OrganizationType,
            Format = format,
            ParametersJson = schedule.ParametersJson,
            RequestedByUserId = actorUserId,
            UseOverride = schedule.UseOverride,
            ViewId = schedule.ViewId
        };

        var exportResult = await _exportService.ExportReportAsync(exportRequest, ct);

        if (!exportResult.Success || exportResult.Data is null)
        {
            var reason = exportResult.ErrorMessage ?? "Export failed.";
            run.Status = "Failed";
            run.FailureReason = reason;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _scheduleRepo.UpdateRunAsync(run, ct);

            await TryAuditAsync(AuditEventFactory.ScheduleRunFailed(
                schedule.TenantId, actorUserId, run.Id, schedule.Id,
                schedule.Name, reason, schedule.ProductCode));

            _log.LogWarning("Schedule run failed: {RunId} schedule={ScheduleId} reason={Reason}",
                run.Id, schedule.Id, reason);
            return run;
        }

        var exportData = exportResult.Data;
        run.ExportId = exportData.ExportId;
        run.GeneratedFileName = exportData.FileName;
        run.GeneratedFileSize = exportData.FileSize;
        run.Status = "Completed";
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        await _scheduleRepo.UpdateRunAsync(run, ct);

        await TryAuditAsync(AuditEventFactory.ScheduleRunCompleted(
            schedule.TenantId, actorUserId, run.Id, schedule.Id,
            schedule.Name, exportData.FileName, exportData.FileSize, schedule.ProductCode));

        var deliveryAdapter = _deliveryAdapters.FirstOrDefault(a =>
            string.Equals(a.MethodName, schedule.DeliveryMethod, StringComparison.OrdinalIgnoreCase));

        if (deliveryAdapter is not null && exportData.FileContent is not null)
        {
            try
            {
                var deliveryResult = await deliveryAdapter.DeliverAsync(
                    exportData.FileContent, exportData.FileName, exportData.ContentType,
                    schedule.DeliveryConfigJson, ct);

                run.DeliveryResultJson = JsonSerializer.Serialize(new
                {
                    deliveryResult.Success,
                    deliveryResult.Method,
                    deliveryResult.Message,
                    deliveryResult.ExternalReferenceId,
                    deliveryResult.DurationMs,
                    deliveryResult.IsRetryable,
                });

                if (deliveryResult.Success)
                {
                    run.Status = "Delivered";
                    run.DeliveredAtUtc = deliveryResult.DeliveredAtUtc;

                    _metrics.IncrementDeliveryCount(schedule.DeliveryMethod, "Success");
                    _metrics.IncrementScheduleRunCount(schedule.TenantId, "Success");

                    await TryAuditAsync(AuditEventFactory.ScheduleDeliveryCompleted(
                        schedule.TenantId, actorUserId, run.Id, schedule.Id,
                        schedule.DeliveryMethod, schedule.ProductCode,
                        externalReferenceId: deliveryResult.ExternalReferenceId,
                        durationMs: deliveryResult.DurationMs));
                }
                else
                {
                    run.Status = "DeliveryFailed";
                    run.FailureReason = deliveryResult.Message;

                    _metrics.IncrementDeliveryCount(schedule.DeliveryMethod, "Failed");
                    _metrics.IncrementScheduleRunCount(schedule.TenantId, "Failed");

                    await TryAuditAsync(AuditEventFactory.ScheduleDeliveryFailed(
                        schedule.TenantId, actorUserId, run.Id, schedule.Id,
                        schedule.DeliveryMethod, deliveryResult.Message ?? "Delivery failed", schedule.ProductCode,
                        externalReferenceId: deliveryResult.ExternalReferenceId,
                        durationMs: deliveryResult.DurationMs));
                }

                await _scheduleRepo.UpdateRunAsync(run, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Delivery failed for run {RunId}", run.Id);
                run.Status = "DeliveryFailed";
                run.FailureReason = $"Delivery exception: {ex.Message}";
                run.DeliveryResultJson = JsonSerializer.Serialize(new { success = false, error = ex.Message });
                await _scheduleRepo.UpdateRunAsync(run, ct);

                await TryAuditAsync(AuditEventFactory.ScheduleDeliveryFailed(
                    schedule.TenantId, actorUserId, run.Id, schedule.Id,
                    schedule.DeliveryMethod, ex.Message, schedule.ProductCode));
            }
        }

        _log.LogInformation("Schedule run completed: {RunId} schedule={ScheduleId} status={Status}",
            run.Id, schedule.Id, run.Status);

        return run;
    }

    internal static DateTimeOffset CalculateNextRun(
        string frequencyType, string? configJson, string timezone, DateTimeOffset fromUtc)
    {
        int hour = 9, minute = 0;
        int dayOfWeek = 1;
        int dayOfMonth = 1;

        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.TryGetProperty("hour", out var h)) hour = h.GetInt32();
                if (doc.RootElement.TryGetProperty("minute", out var m)) minute = m.GetInt32();
                if (doc.RootElement.TryGetProperty("dayOfWeek", out var dow))
                {
                    var dowStr = dow.GetString();
                    if (Enum.TryParse<DayOfWeek>(dowStr, true, out var parsed))
                        dayOfWeek = (int)parsed;
                }
                if (doc.RootElement.TryGetProperty("dayOfMonth", out var dom))
                    dayOfMonth = dom.GetInt32();
            }
            catch
            {
            }
        }

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch { tz = TimeZoneInfo.Utc; }

        var localNow = TimeZoneInfo.ConvertTime(fromUtc, tz);
        DateTimeOffset nextLocal;

        switch (frequencyType.ToLowerInvariant())
        {
            case "daily":
                var todayTarget = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, hour, minute, 0, localNow.Offset);
                nextLocal = localNow >= todayTarget ? todayTarget.AddDays(1) : todayTarget;
                break;

            case "weekly":
                var targetDow = (DayOfWeek)dayOfWeek;
                var daysUntil = ((int)targetDow - (int)localNow.DayOfWeek + 7) % 7;
                var weekTarget = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, hour, minute, 0, localNow.Offset).AddDays(daysUntil);
                nextLocal = (daysUntil == 0 && localNow >= weekTarget) ? weekTarget.AddDays(7) : weekTarget;
                break;

            case "monthly":
                var clampedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(localNow.Year, localNow.Month));
                var monthTarget = new DateTimeOffset(localNow.Year, localNow.Month, clampedDay, hour, minute, 0, localNow.Offset);
                if (localNow >= monthTarget)
                {
                    var nextMonth = localNow.AddMonths(1);
                    clampedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    monthTarget = new DateTimeOffset(nextMonth.Year, nextMonth.Month, clampedDay, hour, minute, 0, localNow.Offset);
                }
                nextLocal = monthTarget;
                break;

            default:
                nextLocal = localNow.AddDays(1);
                break;
        }

        return TimeZoneInfo.ConvertTimeToUtc(nextLocal.DateTime, tz);
    }

    private async Task<bool> IsTenantAssignedAsync(Guid templateId, string tenantId, CancellationToken ct)
    {
        var hasGlobal = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, null, ct);
        if (hasGlobal) return true;

        var hasTenant = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId, null, ct);
        return hasTenant;
    }

    private static string? ValidateCreateRequest(CreateReportScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId)) return "TenantId is required.";
        if (request.ReportTemplateId == Guid.Empty) return "ReportTemplateId is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode)) return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType)) return "OrganizationType is required.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.FrequencyType)) return "FrequencyType is required.";
        if (!SupportedFrequencies.Contains(request.FrequencyType.Trim())) return $"Unsupported FrequencyType: '{request.FrequencyType}'. Supported: Daily, Weekly, Monthly.";
        if (string.IsNullOrWhiteSpace(request.Timezone)) return "Timezone is required.";
        var tzError = ValidateTimezone(request.Timezone);
        if (tzError is not null) return tzError;
        if (string.IsNullOrWhiteSpace(request.ExportFormat)) return "ExportFormat is required.";
        if (!SupportedFormats.Contains(request.ExportFormat.Trim())) return $"Unsupported ExportFormat: '{request.ExportFormat}'. Supported: CSV, XLSX, PDF.";
        if (string.IsNullOrWhiteSpace(request.DeliveryMethod)) return "DeliveryMethod is required.";
        if (!SupportedDeliveryMethods.Contains(request.DeliveryMethod.Trim())) return $"Unsupported DeliveryMethod: '{request.DeliveryMethod}'. Supported: OnScreen, Email, SFTP.";
        var freqError = ValidateFrequencyConfig(request.FrequencyType.Trim(), request.FrequencyConfigJson);
        if (freqError is not null) return freqError;
        return null;
    }

    private static string? ValidateUpdateRequest(UpdateReportScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.FrequencyType)) return "FrequencyType is required.";
        if (!SupportedFrequencies.Contains(request.FrequencyType.Trim())) return $"Unsupported FrequencyType: '{request.FrequencyType}'.";
        if (string.IsNullOrWhiteSpace(request.Timezone)) return "Timezone is required.";
        var tzError = ValidateTimezone(request.Timezone);
        if (tzError is not null) return tzError;
        if (string.IsNullOrWhiteSpace(request.ExportFormat)) return "ExportFormat is required.";
        if (!SupportedFormats.Contains(request.ExportFormat.Trim())) return $"Unsupported ExportFormat: '{request.ExportFormat}'.";
        if (string.IsNullOrWhiteSpace(request.DeliveryMethod)) return "DeliveryMethod is required.";
        if (!SupportedDeliveryMethods.Contains(request.DeliveryMethod.Trim())) return $"Unsupported DeliveryMethod: '{request.DeliveryMethod}'.";
        var freqError = ValidateFrequencyConfig(request.FrequencyType.Trim(), request.FrequencyConfigJson);
        if (freqError is not null) return freqError;
        return null;
    }

    private static string? ValidateTimezone(string timezone)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim());
            return null;
        }
        catch
        {
            return $"Invalid timezone: '{timezone}'.";
        }
    }

    private static string? ValidateFrequencyConfig(string frequencyType, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(configJson); }
        catch (JsonException) { return "FrequencyConfigJson is not valid JSON."; }

        using (doc)
        {
            if (doc.RootElement.TryGetProperty("hour", out var h))
            {
                if (h.ValueKind != JsonValueKind.Number) return "FrequencyConfig 'hour' must be a number.";
                var hour = h.GetInt32();
                if (hour < 0 || hour > 23) return "FrequencyConfig 'hour' must be between 0 and 23.";
            }

            if (doc.RootElement.TryGetProperty("minute", out var m))
            {
                if (m.ValueKind != JsonValueKind.Number) return "FrequencyConfig 'minute' must be a number.";
                var minute = m.GetInt32();
                if (minute < 0 || minute > 59) return "FrequencyConfig 'minute' must be between 0 and 59.";
            }

            if (string.Equals(frequencyType, "Weekly", StringComparison.OrdinalIgnoreCase) &&
                doc.RootElement.TryGetProperty("dayOfWeek", out var dow))
            {
                var dowStr = dow.GetString();
                if (dowStr is null || !Enum.TryParse<DayOfWeek>(dowStr, true, out _))
                    return $"FrequencyConfig 'dayOfWeek' must be a valid day name (Sunday, Monday, ... Saturday).";
            }

            if (string.Equals(frequencyType, "Monthly", StringComparison.OrdinalIgnoreCase) &&
                doc.RootElement.TryGetProperty("dayOfMonth", out var dom))
            {
                if (dom.ValueKind != JsonValueKind.Number) return "FrequencyConfig 'dayOfMonth' must be a number.";
                var day = dom.GetInt32();
                if (day < 1 || day > 31) return "FrequencyConfig 'dayOfMonth' must be between 1 and 31.";
            }
        }

        return null;
    }

    private static string? ValidateDeliveryConfig(string deliveryMethod, string? configJson)
    {
        var method = deliveryMethod.Trim();
        if (string.Equals(method, "OnScreen", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(configJson))
            return $"DeliveryConfigJson is required for delivery method '{method}'.";

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (string.Equals(method, "Email", StringComparison.OrdinalIgnoreCase))
            {
                if (!doc.RootElement.TryGetProperty("recipients", out _))
                    return "Email delivery requires 'recipients' in DeliveryConfigJson.";
            }
            else if (string.Equals(method, "SFTP", StringComparison.OrdinalIgnoreCase))
            {
                if (!doc.RootElement.TryGetProperty("host", out _))
                    return "SFTP delivery requires 'host' in DeliveryConfigJson.";
                if (!doc.RootElement.TryGetProperty("path", out _))
                    return "SFTP delivery requires 'path' in DeliveryConfigJson.";
            }
        }
        catch (JsonException)
        {
            return "DeliveryConfigJson is not valid JSON.";
        }

        return null;
    }

    private static ReportScheduleResponse MapToResponse(ReportSchedule s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        ReportTemplateId = s.ReportTemplateId,
        ProductCode = s.ProductCode,
        OrganizationType = s.OrganizationType,
        Name = s.Name,
        Description = s.Description,
        IsActive = s.IsActive,
        FrequencyType = s.FrequencyType,
        FrequencyConfigJson = s.FrequencyConfigJson,
        Timezone = s.Timezone,
        NextRunAtUtc = s.NextRunAtUtc,
        LastRunAtUtc = s.LastRunAtUtc,
        UseOverride = s.UseOverride,
        ViewId = s.ViewId,
        ExportFormat = s.ExportFormat,
        DeliveryMethod = s.DeliveryMethod,
        DeliveryConfigJson = s.DeliveryConfigJson,
        ParametersJson = s.ParametersJson,
        RequiredFeatureCode = s.RequiredFeatureCode,
        MinimumTierCode = s.MinimumTierCode,
        CreatedAtUtc = s.CreatedAtUtc,
        CreatedByUserId = s.CreatedByUserId,
        UpdatedAtUtc = s.UpdatedAtUtc,
        UpdatedByUserId = s.UpdatedByUserId
    };

    private static ReportScheduleRunResponse MapRunToResponse(ReportScheduleRun r) => new()
    {
        Id = r.Id,
        ReportScheduleId = r.ReportScheduleId,
        ExecutionId = r.ExecutionId,
        ExportId = r.ExportId,
        Status = r.Status,
        ScheduledForUtc = r.ScheduledForUtc,
        StartedAtUtc = r.StartedAtUtc,
        CompletedAtUtc = r.CompletedAtUtc,
        DeliveredAtUtc = r.DeliveredAtUtc,
        FailureReason = r.FailureReason,
        DeliveryResultJson = r.DeliveryResultJson,
        GeneratedFileName = r.GeneratedFileName,
        GeneratedFileSize = r.GeneratedFileSize,
        CreatedAtUtc = r.CreatedAtUtc
    };

    private async Task TryAuditAsync(Reports.Contracts.Audit.AuditEventDto auditEvent)
    {
        try { await _audit.RecordEventAsync(auditEvent); }
        catch (Exception ex) { _log.LogWarning(ex, "Audit hook failed for action {Action}", auditEvent.EventType); }
    }
}
