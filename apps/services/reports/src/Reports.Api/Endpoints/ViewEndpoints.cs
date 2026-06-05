using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Reports.Application.Templates.DTOs;
using Reports.Application.Views;
using Reports.Application.Views.DTOs;

namespace Reports.Api.Endpoints;

public static class ViewEndpoints
{
    public static void MapViewEndpoints(this IEndpointRouteBuilder routes)
    {
        // LS-ID-TNT-010: product-access enforcement for SynqInsights.
        // LS-ID-TNT-022-003: create/update/delete views require ReportsBuild;
        //                     list/read views require ReportsView.
        var viewGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}/views")
            .WithTags("Tenant Report Views")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights);

        // Create a view (save-as-view in builder) → requires ReportsBuild.
        viewGroup.MapPost("/", CreateView)
            .WithName("CreateTenantReportView")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportViewResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Update a view → requires ReportsBuild.
        viewGroup.MapPut("/{viewId:guid}", UpdateView)
            .WithName("UpdateTenantReportView")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Read a single view → requires ReportsView.
        viewGroup.MapGet("/{viewId:guid}", GetViewById)
            .WithName("GetTenantReportView")
            .RequirePermission(PermissionCodes.InsightsReportsView)
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // List views for a template → requires ReportsView (used by report viewer).
        viewGroup.MapGet("/", ListViews)
            .WithName("ListTenantReportViews")
            .RequirePermission(PermissionCodes.InsightsReportsView)
            .Produces<IReadOnlyList<TenantReportViewResponse>>()
            .Produces(StatusCodes.Status403Forbidden);

        // Delete a view → requires ReportsBuild.
        viewGroup.MapDelete("/{viewId:guid}", DeleteView)
            .WithName("DeleteTenantReportView")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateView(
        Guid templateId,
        CreateTenantReportViewRequest request,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.CreateViewAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateView(
        Guid templateId,
        Guid viewId,
        UpdateTenantReportViewRequest request,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.UpdateViewAsync(templateId, viewId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetViewById(
        Guid templateId,
        Guid viewId,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.GetViewByIdAsync(templateId, viewId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListViews(
        Guid templateId,
        ITenantReportViewService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ListViewsAsync(templateId, tenantId ?? string.Empty, ct);
        return ToResult(result);
    }

    private static async Task<IResult> DeleteView(
        Guid templateId,
        Guid viewId,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.DeleteViewAsync(templateId, viewId, ct);
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
            403 => Results.Json(error, statusCode: 403),
            404 => Results.NotFound(error),
            409 => Results.Conflict(error),
            _ => Results.Json(error, statusCode: result.StatusCode)
        };
    }
}
