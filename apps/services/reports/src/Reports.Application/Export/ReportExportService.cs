using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Execution;
using Reports.Application.Execution.DTOs;
using Reports.Application.Export.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Export;
using Reports.Contracts.Observability;
using Reports.Contracts.Storage;

namespace Reports.Application.Export;

public sealed class ReportExportService : IReportExportService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IReportExecutionService _executionService;
    private readonly IEnumerable<IReportExporter> _exporters;
    private readonly IAuditAdapter _audit;
    private readonly IFileStorageAdapter _storage;
    private readonly IReportsMetrics _metrics;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<ReportExportService> _log;

    public ReportExportService(
        IReportExecutionService executionService,
        IEnumerable<IReportExporter> exporters,
        IAuditAdapter audit,
        IFileStorageAdapter storage,
        IReportsMetrics metrics,
        ICurrentTenantContext ctx,
        ILogger<ReportExportService> log)
    {
        _executionService = executionService;
        _exporters = exporters;
        _audit = audit;
        _storage = storage;
        _metrics = metrics;
        _ctx = ctx;
        _log = log;
    }

    public async Task<ServiceResult<ExportReportResponse>> ExportReportAsync(
        ExportReportRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ExportReportResponse>.Forbidden("No authenticated user context for export.");

        var validation = ValidateRequest(request);
        if (validation is not null)
            return ServiceResult<ExportReportResponse>.BadRequest(validation);

        var exporter = _exporters.FirstOrDefault(e =>
            string.Equals(e.FormatName, request.Format.ToString(), StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
            return ServiceResult<ExportReportResponse>.BadRequest(
                $"Unsupported export format: {request.Format}");

        var exportId = Guid.NewGuid();

        await TryAuditAsync(AuditEventFactory.ExportStarted(
            request.TenantId, actorId, exportId,
            request.TemplateId, request.Format.ToString(), request.ProductCode));

        var executeRequest = new ExecuteReportRequest
        {
            TenantId = request.TenantId,
            TemplateId = request.TemplateId,
            ProductCode = request.ProductCode,
            OrganizationType = request.OrganizationType,
            ParametersJson = request.ParametersJson,
            RequestedByUserId = actorId,
            UseOverride = request.UseOverride,
            ViewId = request.ViewId
        };

        var executionResult = await _executionService.ExecuteReportAsync(executeRequest, ct);

        if (!executionResult.Success || executionResult.Data is null)
        {
            var reason = executionResult.ErrorMessage ?? "Execution failed.";
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, actorId, exportId,
                request.TemplateId, request.Format.ToString(), reason, request.ProductCode));

            return ServiceResult<ExportReportResponse>.Fail(
                $"Report execution failed: {reason}", executionResult.StatusCode);
        }

        var execData = executionResult.Data;

        var hasFormatting = execData.Rows.Any(r => r.FormattedValues is not null && r.FormattedValues.Count > 0);

        var exportRows = new List<Dictionary<string, object?>>(execData.Rows.Count);
        foreach (var row in execData.Rows)
        {
            if (hasFormatting && row.FormattedValues is not null && row.FormattedValues.Count > 0)
            {
                var merged = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase);
                foreach (var fv in row.FormattedValues)
                {
                    merged[fv.Key] = fv.Value;
                }
                exportRows.Add(merged);
            }
            else
            {
                exportRows.Add(row.Values);
            }
        }

        var resultSet = new TabularResultSet
        {
            Columns = execData.Columns.Select(c => new TabularColumn
            {
                Key = c.Key,
                Label = c.Label,
                DataType = c.DataType,
                Order = c.Order
            }).ToList(),
            Rows = exportRows,
            TotalRowCount = execData.RowCount,
            WasTruncated = false
        };

        var exportCtx = new ExportContext
        {
            TemplateCode = execData.TemplateCode,
            TemplateName = execData.TemplateName,
            TenantId = request.TenantId,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };

        ExportResult exportResult;
        try
        {
            exportResult = await exporter.ExportAsync(resultSet, exportCtx, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Export {ExportId} failed during {Format} generation", exportId, request.Format);
            _metrics.IncrementExportCount(request.TenantId, request.Format.ToString(), "Failed");
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, actorId, exportId,
                request.TemplateId, request.Format.ToString(), ex.Message, request.ProductCode));

            return ServiceResult<ExportReportResponse>.Fail(
                $"Export generation failed: {ex.Message}");
        }

        if (exportResult.FileSize > MaxFileSizeBytes)
        {
            var msg = $"Export file size ({exportResult.FileSize:N0} bytes) exceeds maximum ({MaxFileSizeBytes:N0} bytes).";
            _log.LogWarning("Export {ExportId}: {Message}", exportId, msg);
            _metrics.IncrementExportCount(request.TenantId, request.Format.ToString(), "Failed");
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, actorId, exportId,
                request.TemplateId, request.Format.ToString(), msg, request.ProductCode));

            return ServiceResult<ExportReportResponse>.BadRequest(msg);
        }

        string? storageKey = null;
        if (_storage.IsEnabled)
        {
            try
            {
                var storageResult = await _storage.UploadAsync(new FileStorageRequest
                {
                    TenantId = request.TenantId,
                    FileName = exportResult.FileName,
                    Content = exportResult.FileContent,
                    ContentType = exportResult.ContentType,
                    SubPath = $"exports/{exportId}",
                }, ct);

                storageKey = storageResult.StorageKey;

                _log.LogInformation(
                    "Export {ExportId}: file stored — key={StorageKey} provider={Provider}",
                    exportId, storageResult.StorageKey, storageResult.Provider);

                await TryAuditAsync(AuditEventFactory.FileStored(
                    request.TenantId, actorId, exportId,
                    storageResult.StorageKey, storageResult.Provider, storageResult.SizeBytes, request.ProductCode));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Export {ExportId}: file storage failed (non-fatal)", exportId);
                await TryAuditAsync(AuditEventFactory.FileStoreFailed(
                    request.TenantId, actorId, exportId, ex.Message, request.ProductCode));
            }
        }

        _metrics.IncrementExportCount(request.TenantId, request.Format.ToString(), "Success");

        await TryAuditAsync(AuditEventFactory.ExportCompleted(
            request.TenantId, actorId, exportId,
            request.TemplateId, request.Format.ToString(),
            execData.RowCount, exportResult.FileSize, request.ProductCode));

        _log.LogInformation(
            "Export completed: {ExportId} format={Format} rows={Rows} size={Size} storageKey={StorageKey}",
            exportId, request.Format, execData.RowCount, exportResult.FileSize, storageKey);

        return ServiceResult<ExportReportResponse>.Ok(new ExportReportResponse
        {
            ExportId = exportId,
            FileName = exportResult.FileName,
            ContentType = exportResult.ContentType,
            FileSize = exportResult.FileSize,
            GeneratedAtUtc = exportCtx.GeneratedAtUtc,
            Format = request.Format,
            Status = "Completed",
            FileContent = exportResult.FileContent,
            StorageKey = storageKey
        });
    }

    private static string? ValidateRequest(ExportReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return "TenantId is required.";
        if (request.TemplateId == Guid.Empty)
            return "TemplateId is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType))
            return "OrganizationType is required.";
        if (!Enum.IsDefined(typeof(ExportFormat), request.Format))
            return $"Invalid export format: {request.Format}";
        return null;
    }

    private async Task TryAuditAsync(Reports.Contracts.Audit.AuditEventDto auditEvent)
    {
        try
        {
            await _audit.RecordEventAsync(auditEvent);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit hook failed for action {Action}", auditEvent.EventType);
        }
    }
}
