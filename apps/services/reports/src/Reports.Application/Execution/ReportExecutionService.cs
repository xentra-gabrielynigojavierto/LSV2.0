using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Execution.DTOs;
using Reports.Application.Formatting;
using Reports.Application.Formulas;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Observability;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Execution;

public sealed class ReportExecutionService : IReportExecutionService
{
    private const int MaxRowCap = 500;

    private readonly IReportRepository _executionRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly ITenantReportOverrideRepository _overrideRepo;
    private readonly ITenantReportViewRepository _viewRepo;
    private readonly IReportDataQueryAdapter _queryAdapter;
    private readonly IAuditAdapter _audit;
    private readonly IReportsMetrics _metrics;
    private readonly ICurrentTenantContext _ctx;
    private readonly ILogger<ReportExecutionService> _log;

    public ReportExecutionService(
        IReportRepository executionRepo,
        ITemplateRepository templateRepo,
        ITemplateAssignmentRepository assignmentRepo,
        ITenantReportOverrideRepository overrideRepo,
        ITenantReportViewRepository viewRepo,
        IReportDataQueryAdapter queryAdapter,
        IAuditAdapter audit,
        IReportsMetrics metrics,
        ICurrentTenantContext ctx,
        ILogger<ReportExecutionService> log)
    {
        _executionRepo = executionRepo;
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _overrideRepo = overrideRepo;
        _viewRepo = viewRepo;
        _queryAdapter = queryAdapter;
        _audit = audit;
        _metrics = metrics;
        _ctx = ctx;
        _log = log;
    }

    public async Task<ServiceResult<ReportExecutionResponse>> ExecuteReportAsync(
        ExecuteReportRequest request, CancellationToken ct)
    {
        // Actor identity is always server-derived — never trusted from request
        var actorId = _ctx.UserId;
        if (actorId is null)
            return ServiceResult<ReportExecutionResponse>.Forbidden("No authenticated user context for execution.");

        var validation = ValidateRequest(request);
        if (validation is not null)
            return ServiceResult<ReportExecutionResponse>.BadRequest(validation);

        var tenantId = request.TenantId.Trim();
        var templateId = request.TemplateId;

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<ReportExecutionResponse>.NotFound($"Template '{templateId}' not found.");

        if (!template.IsActive)
            return ServiceResult<ReportExecutionResponse>.BadRequest($"Template '{templateId}' is not active.");

        if (!string.Equals(template.ProductCode, request.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Product code mismatch: template product is '{template.ProductCode}', request specified '{request.ProductCode}'.");

        var isAssigned = await IsTenantAssignedAsync(templateId, tenantId, ct);
        if (!isAssigned)
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Template '{templateId}' is not assigned to tenant '{tenantId}'.");

        var publishedVersion = await _templateRepo.GetPublishedVersionAsync(templateId, ct);
        if (publishedVersion is null)
            return ServiceResult<ReportExecutionResponse>.NotFound(
                $"Template '{templateId}' has no published version.");

        if (!_queryAdapter.SupportsProduct(request.ProductCode.Trim()))
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Product '{request.ProductCode}' is not supported for report execution.");

        TenantReportOverride? activeOverride = null;
        if (request.UseOverride)
        {
            activeOverride = await _overrideRepo.GetByTenantAndTemplateAsync(tenantId, templateId, ct);
        }

        TenantReportView? activeView = null;
        if (request.ViewId.HasValue && request.ViewId.Value != Guid.Empty)
        {
            activeView = await _viewRepo.GetByIdAsync(request.ViewId.Value, tenantId, ct);
            if (activeView is null || activeView.ReportTemplateId != templateId)
                return ServiceResult<ReportExecutionResponse>.NotFound($"View '{request.ViewId}' not found for template '{templateId}'.");
            if (!activeView.IsActive)
                return ServiceResult<ReportExecutionResponse>.BadRequest($"View '{request.ViewId}' is not active.");
        }

        var definition = BuildExecutionDefinition(template, publishedVersion, activeOverride, activeView);

        var execution = new ReportExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            ReportTemplateId = templateId,
            TemplateVersionNumber = publishedVersion.VersionNumber,
            Status = "Pending"
        };

        await _executionRepo.SaveAsync(execution, ct);

        var execSw = Stopwatch.StartNew();
        try
        {
            await TryAuditAsync(AuditEventFactory.ExecutionStarted(
                tenantId, actorId, execution.Id, templateId, template.Code,
                publishedVersion.VersionNumber, template.ProductCode));

            execution.Status = "Running";
            await _executionRepo.UpdateAsync(execution, ct);
            var queryContext = new ReportQueryContext
            {
                TenantId = tenantId,
                ProductCode = template.ProductCode,
                TemplateId = templateId,
                TemplateCode = template.Code,
                OrganizationType = request.OrganizationType.Trim(),
                VersionNumber = publishedVersion.VersionNumber,
                TemplateBody = publishedVersion.TemplateBody,
                LayoutConfigJson = definition.LayoutConfigJson,
                ColumnConfigJson = definition.ColumnConfigJson,
                FilterConfigJson = definition.FilterConfigJson,
                ParametersJson = request.ParametersJson,
                MaxRows = MaxRowCap
            };

            var queryResult = await _queryAdapter.ExecuteQueryAsync(queryContext, ct);

            if (!queryResult.Success || queryResult.Data is null)
            {
                var reason = queryResult.ErrorMessage ?? "Query adapter returned no data.";
                execution.Status = "Failed";
                execution.FailureReason = reason;
                execution.CompletedAtUtc = DateTimeOffset.UtcNow;
                await _executionRepo.UpdateAsync(execution, ct);

                await TryAuditAsync(AuditEventFactory.ExecutionFailed(
                    tenantId, actorId, execution.Id, templateId, template.Code,
                    reason, template.ProductCode));

                execSw.Stop();
                _metrics.IncrementExecutionCount(tenantId, template.ProductCode, "Failed");
                _metrics.RecordExecutionDuration(tenantId, template.ProductCode, execSw.ElapsedMilliseconds);

                return ServiceResult<ReportExecutionResponse>.Fail(
                    $"Report execution failed: {reason}");
            }

            var resultSet = queryResult.Data;

            var wasTruncated = resultSet.WasTruncated;
            if (resultSet.Rows.Count > MaxRowCap)
            {
                resultSet = new TabularResultSet
                {
                    Columns = resultSet.Columns,
                    Rows = resultSet.Rows.Take(MaxRowCap).ToList(),
                    TotalRowCount = resultSet.TotalRowCount,
                    WasTruncated = true
                };
                wasTruncated = true;
                _log.LogWarning(
                    "Execution {ExecutionId}: result truncated from {OriginalCount} to {MaxRows} rows",
                    execution.Id, queryResult.Data.Rows.Count, MaxRowCap);
            }

            execution.Status = "Completed";
            execution.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _executionRepo.UpdateAsync(execution, ct);

            execSw.Stop();
            _metrics.IncrementExecutionCount(tenantId, template.ProductCode, "Success");
            _metrics.RecordExecutionDuration(tenantId, template.ProductCode, execSw.ElapsedMilliseconds);

            await TryAuditAsync(AuditEventFactory.ExecutionCompleted(
                tenantId, actorId, execution.Id, templateId, template.Code,
                resultSet.TotalRowCount, template.ProductCode));

            _log.LogInformation(
                "Execution completed: {ExecutionId} tenant={TenantId} template={TemplateCode} rows={RowCount} durationMs={DurationMs}",
                execution.Id, tenantId, template.Code, resultSet.TotalRowCount, execSw.ElapsedMilliseconds);

            var columns = resultSet.Columns.ToList();
            var rows = resultSet.Rows;

            var formulas = FormulaEvaluator.ParseConfig(definition.FormulaConfigJson);
            if (formulas is not null && formulas.Count > 0)
            {
                FormulaEvaluator.ApplyFormulas(formulas, rows, columns);
                _log.LogInformation("Applied {FormulaCount} calculated fields for execution {ExecutionId}",
                    formulas.Count, execution.Id);
            }

            var formattingRules = FormattingConfigParser.Parse(definition.FormattingConfigJson);
            List<Dictionary<string, string>>? formattedRows = null;
            if (formattingRules is not null && formattingRules.Count > 0)
            {
                formattedRows = ReportFormattingService.FormatRows(rows, formattingRules, _log);
                _log.LogInformation("Applied {FormattingRuleCount} formatting rules for execution {ExecutionId}",
                    formattingRules.Count, execution.Id);
            }

            var responseRows = new List<ReportRowResponse>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                responseRows.Add(new ReportRowResponse
                {
                    Values = rows[i],
                    FormattedValues = formattedRows?[i]
                });
            }

            var response = new ReportExecutionResponse
            {
                ExecutionId = execution.Id,
                TenantId = tenantId,
                TemplateId = templateId,
                TemplateCode = template.Code,
                TemplateName = definition.EffectiveName,
                PublishedVersionNumber = publishedVersion.VersionNumber,
                BaseTemplateVersionNumber = definition.BaseTemplateVersionNumber,
                HasOverride = definition.HasOverride,
                ViewId = definition.ViewId,
                ViewName = definition.ViewName,
                Columns = columns.Select(c => new ReportColumnResponse
                {
                    Key = c.Key,
                    Label = c.Label,
                    DataType = c.DataType,
                    Order = c.Order
                }).ToList(),
                Rows = responseRows,
                RowCount = resultSet.TotalRowCount,
                ExecutedAtUtc = execution.CompletedAtUtc!.Value,
                ExecutedByUserId = execution.UserId,
                Status = execution.Status
            };

            return ServiceResult<ReportExecutionResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Execution failed: {ExecutionId} tenant={TenantId} template={TemplateId}",
                execution.Id, tenantId, templateId);

            execution.Status = "Failed";
            execution.FailureReason = ex.Message;
            execution.CompletedAtUtc = DateTimeOffset.UtcNow;

            try { await _executionRepo.UpdateAsync(execution, ct); }
            catch (Exception updateEx) { _log.LogWarning(updateEx, "Failed to update execution status after failure"); }

            execSw.Stop();
            _metrics.IncrementExecutionCount(tenantId, template.ProductCode, "Failed");
            _metrics.RecordExecutionDuration(tenantId, template.ProductCode, execSw.ElapsedMilliseconds);

            await TryAuditAsync(AuditEventFactory.ExecutionFailed(
                tenantId, actorId, execution.Id, templateId, template.Code,
                ex.Message, template.ProductCode));

            return ServiceResult<ReportExecutionResponse>.Fail(
                $"Report execution failed unexpectedly: {ex.Message}");
        }
    }

    public async Task<ServiceResult<ReportExecutionSummaryResponse>> GetExecutionByIdAsync(
        Guid executionId, CancellationToken ct)
    {
        var execution = await _executionRepo.GetByIdAsync(executionId, ct);
        if (execution is null)
            return ServiceResult<ReportExecutionSummaryResponse>.NotFound(
                $"Execution '{executionId}' not found.");

        if (!string.Equals(execution.TenantId, _ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportExecutionSummaryResponse>.NotFound(
                $"Execution '{executionId}' not found.");

        return ServiceResult<ReportExecutionSummaryResponse>.Ok(new ReportExecutionSummaryResponse
        {
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            TemplateId = execution.ReportTemplateId,
            TemplateVersionNumber = execution.TemplateVersionNumber,
            Status = execution.Status,
            FailureReason = execution.FailureReason,
            CreatedAtUtc = execution.CreatedAtUtc,
            CompletedAtUtc = execution.CompletedAtUtc
        });
    }

    private static ExecutionDefinition BuildExecutionDefinition(
        ReportTemplate template,
        ReportTemplateVersion publishedVersion,
        TenantReportOverride? activeOverride,
        TenantReportView? activeView = null)
    {
        return new ExecutionDefinition
        {
            TemplateId = template.Id,
            TemplateCode = template.Code,
            EffectiveName = activeView?.Name ?? activeOverride?.NameOverride ?? template.Name,
            EffectiveDescription = activeView?.Description ?? activeOverride?.DescriptionOverride ?? template.Description,
            ProductCode = template.ProductCode,
            OrganizationType = template.OrganizationType,
            PublishedVersionNumber = publishedVersion.VersionNumber,
            TemplateBody = publishedVersion.TemplateBody,
            HasOverride = activeOverride is not null,
            BaseTemplateVersionNumber = activeOverride?.BaseTemplateVersionNumber,
            OverrideId = activeOverride?.Id,
            LayoutConfigJson = activeView?.LayoutConfigJson ?? activeOverride?.LayoutConfigJson,
            ColumnConfigJson = activeView?.ColumnConfigJson ?? activeOverride?.ColumnConfigJson,
            FilterConfigJson = activeView?.FilterConfigJson ?? activeOverride?.FilterConfigJson,
            ViewId = activeView?.Id,
            ViewName = activeView?.Name,
            FormulaConfigJson = activeView?.FormulaConfigJson,
            FormattingConfigJson = activeView?.FormattingConfigJson
        };
    }

    private async Task<bool> IsTenantAssignedAsync(Guid templateId, string tenantId, CancellationToken ct)
    {
        var hasGlobal = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, null, ct);
        if (hasGlobal) return true;

        var hasTenant = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId, null, ct);
        return hasTenant;
    }

    private static string? ValidateRequest(ExecuteReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return "TenantId is required.";
        if (request.TemplateId == Guid.Empty)
            return "TemplateId is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType))
            return "OrganizationType is required.";
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
