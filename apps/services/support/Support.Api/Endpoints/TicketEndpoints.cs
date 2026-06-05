using FluentValidation;
using Support.Api.Auth;
using Support.Api.Dtos;
using Support.Api.Services;
using Support.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/tickets").WithTags("Tickets");

        grp.MapPost("/", async (
            [FromBody] CreateTicketRequest req,
            IValidator<CreateTicketRequest> validator,
            ITicketService svc,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(req, ct);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
            try
            {
                var created = await svc.CreateAsync(req, ct);
                return Results.Created($"/support/api/tickets/{created.Id}", created);
            }
            catch (TenantMissingException)
            {
                return Results.Problem(statusCode: 400, title: "tenant_id required");
            }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .Produces<TicketResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        grp.MapGet("/", async (
            [AsParameters] TicketListQueryBinder bind,
            ITicketService svc,
            CancellationToken ct) =>
        {
            try
            {
                var result = await svc.ListAsync(bind.ToQuery(), ct);
                return Results.Ok(result);
            }
            catch (TenantMissingException)
            {
                return Results.Problem(statusCode: 400, title: "Tenant context required");
            }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<PagedResponse<TicketResponse>>();

        grp.MapGet("/{id:guid}", async (Guid id, ITicketService svc, CancellationToken ct) =>
        {
            try
            {
                var t = await svc.GetAsync(id, ct);
                return t is null ? Results.NotFound() : Results.Ok(t);
            }
            catch (TenantMissingException)
            {
                return Results.Problem(statusCode: 400, title: "Tenant context required");
            }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<TicketResponse>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateTicketRequest req,
            IValidator<UpdateTicketRequest> validator,
            ITicketService svc,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(req, ct);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
            try
            {
                var updated = await svc.UpdateAsync(id, req, ct);
                return Results.Ok(updated);
            }
            catch (TicketNotFoundException) { return Results.NotFound(); }
            catch (InvalidStatusTransitionException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .Produces<TicketResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        grp.MapPut("/{id:guid}/assignment", async (
            Guid id,
            [FromBody] AssignTicketRequest req,
            IValidator<AssignTicketRequest> validator,
            ITicketService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var updated = await svc.AssignAsync(id, req, ct);
                return Results.Ok(updated);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
            catch (QueueNotFoundException) { return Results.NotFound(new { error = "Assigned queue not found." }); }
            catch (QueueInactiveException) { return Results.BadRequest(new { error = "Assigned queue is inactive." }); }
        })
        .RequireAuthorization(SupportPolicies.SupportInternal)
        .Produces<TicketResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
    }
}

public class TicketListQueryBinder
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public TicketSeverity? Severity { get; set; }
    public TicketSource? Source { get; set; }
    public string? ProductCode { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public string? AssignedUserId { get; set; }
    public Guid? AssignedQueueId { get; set; }
    public bool? Unassigned { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    /// <summary>
    /// Optional tenant filter honoured only for PlatformAdmin callers.
    /// Tenant-scoped users are always filtered to their own tenant via JWT claim.
    /// </summary>
    public string? TenantId { get; set; }

    public TicketListQuery ToQuery() => new()
    {
        Status = Status,
        Priority = Priority,
        Severity = Severity,
        Source = Source,
        ProductCode = ProductCode,
        Category = Category,
        Search = Search,
        AssignedUserId = AssignedUserId,
        AssignedQueueId = AssignedQueueId,
        Unassigned = Unassigned,
        TenantId = TenantId,
        Page = Page ?? 1,
        PageSize = PageSize ?? 25,
    };
}
