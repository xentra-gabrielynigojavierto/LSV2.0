using FluentValidation;
using Support.Api.Auth;
using Support.Api.Dtos;
using Support.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

public static class ProductRefEndpoints
{
    public static void MapProductRefEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/tickets/{id:guid}").WithTags("Product References");

        grp.MapPost("/product-refs", async (
            Guid id,
            [FromBody] CreateProductReferenceRequest req,
            IValidator<CreateProductReferenceRequest> validator,
            ITicketProductReferenceService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var r = await svc.AddAsync(id, req, ct);
                return Results.Created($"/support/api/tickets/{id}/product-refs/{r.Id}", r);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
            catch (DuplicateProductReferenceException)
            {
                return Results.Problem(statusCode: 409, title: "Duplicate product reference",
                    detail: "A product reference with the same product_code, entity_type, and entity_id already exists for this ticket.");
            }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .Produces<ProductReferenceResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        grp.MapGet("/product-refs", async (
            Guid id,
            [FromQuery(Name = "product_code")] string? productCode,
            [FromQuery(Name = "entity_type")] string? entityType,
            ITicketProductReferenceService svc,
            CancellationToken ct) =>
        {
            try
            {
                var items = await svc.ListAsync(id, productCode, entityType, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<ProductReferenceResponse>>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapDelete("/product-refs/{refId:guid}", async (
            Guid id,
            Guid refId,
            ITicketProductReferenceService svc,
            CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteAsync(id, refId, ct);
                return Results.NoContent();
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
            catch (ProductReferenceNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
