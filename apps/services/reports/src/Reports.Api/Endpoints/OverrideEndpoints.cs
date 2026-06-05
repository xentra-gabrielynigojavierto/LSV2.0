using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Reports.Application.Overrides;
using Reports.Application.Overrides.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class OverrideEndpoints
{
    public static void MapOverrideEndpoints(this IEndpointRouteBuilder routes)
    {
        // LS-ID-TNT-010: product-access enforcement for SynqInsights.
        // LS-ID-TNT-022-003: write operations require ReportsBuild; read operations
        //                     are gated at product access (SynqInsights) only.
        var overrideGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}/overrides")
            .WithTags("Tenant Report Overrides")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights);

        // Create override (builder save) → requires ReportsBuild.
        overrideGroup.MapPost("/", CreateOverride)
            .WithName("CreateTenantReportOverride")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportOverrideResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Update override → requires ReportsBuild.
        overrideGroup.MapPut("/{overrideId:guid}", UpdateOverride)
            .WithName("UpdateTenantReportOverride")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Read a specific override → product access only (read-only, used by viewer/builder).
        overrideGroup.MapGet("/{overrideId:guid}", GetOverrideById)
            .WithName("GetTenantReportOverride")
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status404NotFound);

        // List overrides → product access only.
        overrideGroup.MapGet("/", ListOverrides)
            .WithName("ListTenantReportOverrides")
            .Produces<IReadOnlyList<TenantReportOverrideResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        // Deactivate override → requires ReportsBuild.
        overrideGroup.MapDelete("/{overrideId:guid}", DeactivateOverride)
            .WithName("DeactivateTenantReportOverride")
            .RequirePermission(PermissionCodes.InsightsReportsBuild)
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // ── Effective report resolver ─────────────────────────────────────────
        // Used by the Report Viewer and Builder to load the effective definition.
        // Requires ReportsView: the minimal permission for any Insights reader.
        var effectiveGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}")
            .WithTags("Tenant Effective Report")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights);

        effectiveGroup.MapGet("/effective", ResolveEffectiveReport)
            .WithName("ResolveEffectiveReport")
            .RequirePermission(PermissionCodes.InsightsReportsView)
            .Produces<TenantEffectiveReportResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateOverride(
        Guid templateId,
        CreateTenantReportOverrideRequest request,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.CreateOverrideAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateOverride(
        Guid templateId,
        Guid overrideId,
        UpdateTenantReportOverrideRequest request,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.UpdateOverrideAsync(templateId, overrideId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetOverrideById(
        Guid templateId,
        Guid overrideId,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.GetOverrideByIdAsync(templateId, overrideId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListOverrides(
        Guid templateId,
        ITenantReportOverrideService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ListOverridesAsync(templateId, tenantId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> DeactivateOverride(
        Guid templateId,
        Guid overrideId,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.DeactivateOverrideAsync(templateId, overrideId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ResolveEffectiveReport(
        Guid templateId,
        ITenantReportOverrideService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ResolveEffectiveReportAsync(templateId, tenantId ?? string.Empty, ct);
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
