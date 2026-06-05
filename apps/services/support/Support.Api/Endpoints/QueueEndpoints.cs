using FluentValidation;
using Support.Api.Auth;
using Support.Api.Dtos;
using Support.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/queues").WithTags("Queues");

        grp.MapPost("/", async (
            [FromBody] CreateQueueRequest req,
            IValidator<CreateQueueRequest> validator,
            IQueueService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var q = await svc.CreateAsync(req, ct);
                return Results.Created($"/support/api/queues/{q.Id}", q);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (QueueNameConflictException) { return Results.Conflict(new { error = "A queue with that name already exists." }); }
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces<QueueResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        grp.MapGet("/", async (
            [FromQuery] string? productCode,
            [FromQuery] bool? isActive,
            [FromQuery] string? search,
            IQueueService svc,
            CancellationToken ct) =>
        {
            try
            {
                var items = await svc.ListAsync(new QueueListQuery
                {
                    ProductCode = productCode,
                    IsActive = isActive,
                    Search = search,
                }, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<QueueResponse>>();

        grp.MapGet("/{queueId:guid}", async (Guid queueId, IQueueService svc, CancellationToken ct) =>
        {
            try
            {
                var q = await svc.GetAsync(queueId, ct);
                return q is null ? Results.NotFound() : Results.Ok(q);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<QueueResponse>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapPut("/{queueId:guid}", async (
            Guid queueId,
            [FromBody] UpdateQueueRequest req,
            IValidator<UpdateQueueRequest> validator,
            IQueueService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var q = await svc.UpdateAsync(queueId, req, ct);
                return Results.Ok(q);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (QueueNotFoundException) { return Results.NotFound(); }
            catch (QueueNameConflictException) { return Results.Conflict(new { error = "A queue with that name already exists." }); }
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces<QueueResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        grp.MapPost("/{queueId:guid}/members", async (
            Guid queueId,
            [FromBody] AddQueueMemberRequest req,
            IValidator<AddQueueMemberRequest> validator,
            IQueueService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var m = await svc.AddMemberAsync(queueId, req, ct);
                return Results.Created($"/support/api/queues/{queueId}/members/{m.Id}", m);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (QueueNotFoundException) { return Results.NotFound(); }
            catch (QueueMemberConflictException) { return Results.Conflict(new { error = "User is already an active member of this queue." }); }
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces<QueueMemberResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        grp.MapGet("/{queueId:guid}/members", async (
            Guid queueId,
            IQueueService svc,
            CancellationToken ct) =>
        {
            try
            {
                var items = await svc.ListMembersAsync(queueId, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (QueueNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<QueueMemberResponse>>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapDelete("/{queueId:guid}/members/{memberId:guid}", async (
            Guid queueId,
            Guid memberId,
            IQueueService svc,
            CancellationToken ct) =>
        {
            try
            {
                await svc.RemoveMemberAsync(queueId, memberId, ct);
                return Results.NoContent();
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (QueueNotFoundException) { return Results.NotFound(); }
            catch (QueueMemberNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportManage)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
