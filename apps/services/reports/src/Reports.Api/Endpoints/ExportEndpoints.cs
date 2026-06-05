using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Reports.Application.Export;
using Reports.Application.Export.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder routes)
    {
        // LS-ID-TNT-010: product-access enforcement for SynqInsights.
        // LS-ID-TNT-022-003: per-action permission enforcement added.
        var group = routes.MapGroup("/api/v1/report-exports")
            .WithTags("Report Exports")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights);

        // Export a report → requires ReportsExport.
        group.MapPost("/", ExportReport)
            .WithName("ExportReport")
            .RequirePermission(PermissionCodes.InsightsReportsExport)
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> ExportReport(
        ExportReportRequest request,
        IReportExportService service,
        CancellationToken ct)
    {
        var result = await service.ExportReportAsync(request, ct);

        if (!result.Success)
        {
            var error = new { error = result.ErrorMessage };
            return result.StatusCode switch
            {
                400 => Results.BadRequest(error),
                404 => Results.NotFound(error),
                409 => Results.Conflict(error),
                _ => Results.Json(error, statusCode: result.StatusCode)
            };
        }

        var data = result.Data!;
        return Results.File(
            data.FileContent,
            data.ContentType,
            data.FileName);
    }
}
