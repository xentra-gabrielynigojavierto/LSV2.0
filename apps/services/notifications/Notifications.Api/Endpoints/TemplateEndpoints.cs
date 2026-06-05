using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/templates").WithTags("Templates");

        group.MapGet("/", async (HttpContext context, ITemplateService service, int? limit, int? offset) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.ListByTenantAsync(tenantId, limit ?? 50, offset ?? 0);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (HttpContext context, ITemplateService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetByIdAsync(id);
            if (result == null) return Results.NotFound();
            if (result.TenantId.HasValue && result.TenantId != tenantId) return Results.NotFound();
            return Results.Ok(result);
        });

        group.MapPost("/", async (HttpContext context, ITemplateService service, CreateTemplateDto request) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.CreateAsync(tenantId, request);
            return Results.Created($"/v1/templates/{result.Id}", result);
        });

        group.MapPut("/{id:guid}", async (HttpContext context, ITemplateService service, Guid id, UpdateTemplateDto request) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await service.GetByIdAsync(id);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId.HasValue && existing.TenantId != tenantId) return Results.NotFound();
            var result = await service.UpdateAsync(id, request);
            return Results.Ok(result);
        });

        group.MapDelete("/{id:guid}", async (HttpContext context, ITemplateService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await service.GetByIdAsync(id);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId.HasValue && existing.TenantId != tenantId) return Results.NotFound();
            await service.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapPost("/{templateId:guid}/versions", async (HttpContext context, ITemplateService service, Guid templateId, CreateTemplateVersionDto request) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await service.GetByIdAsync(templateId);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId.HasValue && existing.TenantId != tenantId) return Results.NotFound();
            var result = await service.CreateVersionAsync(templateId, request);
            return Results.Created($"/v1/templates/{templateId}/versions/{result.Id}", result);
        });

        group.MapGet("/{templateId:guid}/versions", async (HttpContext context, ITemplateService service, Guid templateId) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await service.GetByIdAsync(templateId);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId.HasValue && existing.TenantId != tenantId) return Results.NotFound();
            var result = await service.ListVersionsAsync(templateId);
            return Results.Ok(result);
        });

        group.MapPost("/{templateId:guid}/versions/{versionId:guid}/publish", async (HttpContext context, ITemplateService service, Guid templateId, Guid versionId) =>
        {
            var tenantId = context.GetTenantId();
            var existing = await service.GetByIdAsync(templateId);
            if (existing == null) return Results.NotFound();
            if (existing.TenantId.HasValue && existing.TenantId != tenantId) return Results.NotFound();
            var result = await service.PublishVersionAsync(templateId, versionId, null);
            return Results.Ok(result);
        });
    }

    public static void MapGlobalTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/templates/global").WithTags("GlobalTemplates");

        group.MapGet("/", async (ITemplateService service, int? limit, int? offset) =>
        {
            var result = await service.ListGlobalAsync(limit ?? 50, offset ?? 0);
            return Results.Ok(result);
        });

        group.MapPost("/", async (ITemplateService service, CreateTemplateDto request) =>
        {
            var result = await service.CreateAsync(null, request);
            return Results.Created($"/v1/templates/{result.Id}", result);
        });
    }
}
