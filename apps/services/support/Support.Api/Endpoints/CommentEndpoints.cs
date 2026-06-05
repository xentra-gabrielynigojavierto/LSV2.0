using FluentValidation;
using Support.Api.Auth;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/tickets/{id:guid}").WithTags("Comments");

        grp.MapPost("/comments", async (
            Guid id,
            [FromBody] CreateCommentRequest req,
            IValidator<CreateCommentRequest> validator,
            ICommentService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var c = await svc.AddAsync(id, req, ct);
                return Results.Created($"/support/api/tickets/{id}/comments/{c.Id}", c);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .Produces<CommentResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        grp.MapGet("/comments", async (
            Guid id,
            [FromQuery] CommentVisibility? visibility,
            [FromQuery(Name = "comment_type")] CommentType? commentType,
            ICommentService svc,
            CancellationToken ct) =>
        {
            try
            {
                var items = await svc.ListAsync(id, visibility, commentType, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<CommentResponse>>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapGet("/timeline", async (Guid id, ICommentService svc, CancellationToken ct) =>
        {
            try
            {
                var items = await svc.TimelineAsync(id, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<TimelineItem>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
