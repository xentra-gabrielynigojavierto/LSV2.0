using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Reports.Application.Assignments;
using Reports.Application.Assignments.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this IEndpointRouteBuilder routes)
    {
        // Template assignment management — platform/tenant admin only.
        // No additional permission gate required; PlatformOrTenantAdmin implies full access.
        var assignmentGroup = routes.MapGroup("/api/v1/templates/{templateId:guid}/assignments")
            .WithTags("Template Assignments")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        assignmentGroup.MapPost("/", CreateAssignment)
            .WithName("CreateTemplateAssignment")
            .Produces<TemplateAssignmentResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        assignmentGroup.MapPut("/{assignmentId:guid}", UpdateAssignment)
            .WithName("UpdateTemplateAssignment")
            .Produces<TemplateAssignmentResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        assignmentGroup.MapGet("/", ListAssignments)
            .WithName("ListTemplateAssignments")
            .Produces<IReadOnlyList<TemplateAssignmentResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        assignmentGroup.MapGet("/{assignmentId:guid}", GetAssignmentById)
            .WithName("GetTemplateAssignment")
            .Produces<TemplateAssignmentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        // ── Tenant report catalog ─────────────────────────────────────────────
        // LS-ID-TNT-022-003: Catalog endpoint now requires SynqInsights product access
        //   and the ReportsView permission. This closes the gap where any authenticated
        //   user could query the tenant's report catalog without Insights access.
        var catalogGroup = routes.MapGroup("/api/v1/tenant-templates")
            .WithTags("Tenant Template Catalog")
            .RequireAuthorization()
            .RequireProductAccess(ProductCodes.SynqInsights)
            .RequirePermission(PermissionCodes.InsightsReportsView);

        catalogGroup.MapGet("/", ResolveTenantCatalog)
            .WithName("ResolveTenantCatalog")
            .Produces<IReadOnlyList<TenantTemplateCatalogItemResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> CreateAssignment(
        Guid templateId,
        CreateTemplateAssignmentRequest request,
        ITemplateAssignmentService service,
        CancellationToken ct)
    {
        var result = await service.CreateAssignmentAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateAssignment(
        Guid templateId,
        Guid assignmentId,
        UpdateTemplateAssignmentRequest request,
        ITemplateAssignmentService service,
        CancellationToken ct)
    {
        var result = await service.UpdateAssignmentAsync(templateId, assignmentId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListAssignments(
        Guid templateId,
        ITemplateAssignmentService service,
        CancellationToken ct)
    {
        var result = await service.ListAssignmentsAsync(templateId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetAssignmentById(
        Guid templateId,
        Guid assignmentId,
        ITemplateAssignmentService service,
        CancellationToken ct)
    {
        var result = await service.GetAssignmentByIdAsync(templateId, assignmentId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ResolveTenantCatalog(
        ITemplateAssignmentService service,
        string? tenantId = null,
        string? productCode = null,
        string? organizationType = null,
        CancellationToken ct = default)
    {
        var query = new TenantTemplateCatalogQuery
        {
            TenantId = tenantId ?? string.Empty,
            ProductCode = productCode ?? string.Empty,
            OrganizationType = organizationType ?? string.Empty
        };
        var result = await service.ResolveTenantCatalogAsync(query, ct);
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
