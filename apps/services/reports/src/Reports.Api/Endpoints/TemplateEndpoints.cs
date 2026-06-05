using BuildingBlocks.Authorization;
using Reports.Application.Templates;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/templates")
            .WithTags("Templates")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        group.MapPost("/", CreateTemplate)
            .WithName("CreateTemplate")
            .Produces<TemplateResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{templateId:guid}", UpdateTemplate)
            .WithName("UpdateTemplate")
            .Produces<TemplateResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{templateId:guid}", GetTemplateById)
            .WithName("GetTemplateById")
            .Produces<TemplateResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListTemplates)
            .WithName("ListTemplates")
            .Produces<IReadOnlyList<TemplateResponse>>();

        group.MapPost("/{templateId:guid}/versions", CreateVersion)
            .WithName("CreateTemplateVersion")
            .Produces<TemplateVersionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{templateId:guid}/versions", ListVersions)
            .WithName("ListTemplateVersions")
            .Produces<IReadOnlyList<TemplateVersionResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{templateId:guid}/versions/latest", GetLatestVersion)
            .WithName("GetLatestVersion")
            .Produces<TemplateVersionResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{templateId:guid}/versions/published", GetPublishedVersion)
            .WithName("GetPublishedVersion")
            .Produces<TemplateVersionResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{templateId:guid}/versions/{versionNumber:int}/publish", PublishVersion)
            .WithName("PublishVersion")
            .Produces<TemplateVersionResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateTemplate(
        CreateTemplateRequest request,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.CreateTemplateAsync(request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateTemplate(
        Guid templateId,
        UpdateTemplateRequest request,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.UpdateTemplateAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetTemplateById(
        Guid templateId,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.GetTemplateByIdAsync(templateId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListTemplates(
        ITemplateManagementService service,
        string? productCode = null,
        string? organizationType = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await service.ListTemplatesAsync(productCode, organizationType, page, pageSize, ct);
        return ToResult(result);
    }

    private static async Task<IResult> CreateVersion(
        Guid templateId,
        CreateTemplateVersionRequest request,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.CreateVersionAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListVersions(
        Guid templateId,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.ListVersionsAsync(templateId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetLatestVersion(
        Guid templateId,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.GetLatestVersionAsync(templateId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetPublishedVersion(
        Guid templateId,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.GetPublishedVersionAsync(templateId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> PublishVersion(
        Guid templateId,
        int versionNumber,
        PublishTemplateVersionRequest request,
        ITemplateManagementService service,
        CancellationToken ct)
    {
        var result = await service.PublishVersionAsync(templateId, versionNumber, request, ct);
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
