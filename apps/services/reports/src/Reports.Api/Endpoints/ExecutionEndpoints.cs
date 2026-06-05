using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Reports.Application.Execution;
using Reports.Application.Execution.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class ExecutionEndpoints
{
    public static void MapExecutionEndpoints(this IEndpointRouteBuilder routes)
    {
        // LS-ID-TNT-010: product-access enforcement for SynqInsights.
        // LS-ID-TNT-022-003: per-action permission enforcement added.
        var group = routes.MapGroup("/api/v1/report-executions")
            .WithTags("Report Executions")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights);

        // Execute (run) a report → requires ReportsRun.
        group.MapPost("/", ExecuteReport)
            .WithName("ExecuteReport")
            .RequirePermission(PermissionCodes.InsightsReportsRun)
            .Produces<ReportExecutionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        // Read an existing execution result → requires ReportsView.
        group.MapGet("/{executionId:guid}", GetExecution)
            .WithName("GetExecution")
            .RequirePermission(PermissionCodes.InsightsReportsView)
            .Produces<ReportExecutionSummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ExecuteReport(
        ExecuteReportRequest request,
        IReportExecutionService service,
        CancellationToken ct)
    {
        var result = await service.ExecuteReportAsync(request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetExecution(
        Guid executionId,
        IReportExecutionService service,
        CancellationToken ct)
    {
        var result = await service.GetExecutionByIdAsync(executionId, ct);
        return ToResult(result);
    }

    private static IResult ToResult<T>(ServiceResult<T> result)
    {
        if (result.Success)
        {
            return result.StatusCode == 201
                ? Results.Created((string?)null, result.Data)
                : Results.Ok(result.Data);
        }

        var error = new { error = result.ErrorMessage };
        return result.StatusCode switch
        {
            400 => Results.BadRequest(error),
            404 => Results.NotFound(error),
            409 => Results.Conflict(error),
            _ => Results.Json(error, statusCode: result.StatusCode)
        };
    }
}
