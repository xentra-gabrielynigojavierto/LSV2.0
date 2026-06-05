using FluentValidation;
using Support.Api.Auth;
using Support.Api.Dtos;
using Support.Api.Services;
using Support.Api.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Support.Api.Endpoints;

/// <summary>
/// Customer-facing ticket endpoints — requires CustomerAccess policy (role: ExternalCustomer).
///
/// Security layers (outer → inner):
///   1. JWT authentication         — 401 if no/invalid token
///   2. Rate limiter               — 429 after 60 req/min per external_customer_id (SUP-TNT-05)
///   3. CustomerAccess policy      — 403 if role ≠ ExternalCustomer
///   4. Tenant resolution          — tenantId from JWT only (Production)
///   5. Input validation           — 400 for invalid page/pageSize or malformed body
///   6. Mode gate                  — 403 if tenant InternalOnly or portal disabled (SUP-TNT-04)
///   7. Ownership enforcement      — service: tenantId + externalCustomerId + CustomerVisible
///
/// Author identity for comments comes from JWT claims (email, name), never from the request body.
/// </summary>
public static class CustomerTicketEndpoints
{
    private const string CustomerIdClaim = "external_customer_id";

    private const string ModeDisabledTitle =
        "Customer support portal is not enabled for this tenant.";

    private const int MaxPageSize = 100;

    public static void MapCustomerTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/customer/tickets")
            .WithTags("Customer Tickets")
            .RequireAuthorization(SupportPolicies.CustomerAccess)
            .RequireRateLimiting(RateLimitPolicies.CustomerEndpoints);

        // ── GET /support/api/customer/tickets ──────────────────────────────────
        grp.MapGet("/", async (
            HttpContext ctx,
            ITicketService svc,
            ISupportTenantSettingsService settingsSvc,
            ILoggerFactory logFactory,
            [FromQuery] int? page,
            [FromQuery(Name = "page_size")] int? pageSize,
            CancellationToken ct) =>
        {
            // SUP-TNT-05: Validate pagination parameters before any DB call.
            var effectivePage     = page     ?? 1;
            var effectivePageSize = pageSize ?? 25;

            if (effectivePage < 1)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page"] = ["page must be \u2265 1."]
                });

            if (effectivePageSize < 1 || effectivePageSize > MaxPageSize)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page_size"] = [$"page_size must be between 1 and {MaxPageSize}."]
                });

            var (tenantId, customerId, err) = ResolveCustomerContext(ctx);
            if (err is not null) return err;

            if (!await settingsSvc.IsCustomerSupportEnabledAsync(tenantId!, ct))
            {
                logFactory.CreateLogger("CustomerTicketEndpoints").LogDebug(
                    "Customer portal not enabled. TenantId={TenantId} CustomerId={CustomerId}",
                    tenantId, customerId);
                return Results.Problem(statusCode: 403, title: ModeDisabledTitle);
            }

            var result = await svc.ListCustomerTicketsAsync(
                tenantId!, customerId!.Value, effectivePage, effectivePageSize, ct);
            return Results.Ok(result);
        })
        .Produces<PagedResponse<TicketResponse>>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem();

        // ── GET /support/api/customer/tickets/{id} ─────────────────────────────
        grp.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext ctx,
            ITicketService svc,
            ISupportTenantSettingsService settingsSvc,
            ILoggerFactory logFactory,
            CancellationToken ct) =>
        {
            var (tenantId, customerId, err) = ResolveCustomerContext(ctx);
            if (err is not null) return err;

            if (!await settingsSvc.IsCustomerSupportEnabledAsync(tenantId!, ct))
            {
                logFactory.CreateLogger("CustomerTicketEndpoints").LogDebug(
                    "Customer portal not enabled. TenantId={TenantId} CustomerId={CustomerId}",
                    tenantId, customerId);
                return Results.Problem(statusCode: 403, title: ModeDisabledTitle);
            }

            var ticket = await svc.GetCustomerTicketAsync(tenantId!, customerId!.Value, id, ct);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        })
        .Produces<TicketResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);

        // ── POST /support/api/customer/tickets/{id}/comments ───────────────────
        grp.MapPost("/{id:guid}/comments", async (
            Guid id,
            [FromBody] CustomerAddCommentRequest req,
            IValidator<CustomerAddCommentRequest> validator,
            HttpContext ctx,
            ICommentService svc,
            ISupportTenantSettingsService settingsSvc,
            ILoggerFactory logFactory,
            CancellationToken ct) =>
        {
            // SUP-TNT-05: Full FluentValidation (required + max length 8000).
            var validation = await validator.ValidateAsync(req, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var (tenantId, customerId, err) = ResolveCustomerContext(ctx);
            if (err is not null) return err;

            if (!await settingsSvc.IsCustomerSupportEnabledAsync(tenantId!, ct))
            {
                logFactory.CreateLogger("CustomerTicketEndpoints").LogDebug(
                    "Customer portal not enabled. TenantId={TenantId} CustomerId={CustomerId}",
                    tenantId, customerId);
                return Results.Problem(statusCode: 403, title: ModeDisabledTitle);
            }

            var authorEmail = ctx.User.FindFirst("email")?.Value;
            var authorName  = ctx.User.FindFirst("name")?.Value;

            try
            {
                var comment = await svc.AddCustomerCommentAsync(
                    tenantId!, customerId!.Value, id,
                    req.Body, authorEmail, authorName, ct);
                return Results.Created($"/support/api/customer/tickets/{id}/comments/{comment.Id}", comment);
            }
            catch (TicketNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .Produces<CommentResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem();
    }

    /// <summary>
    /// Resolves tenantId and externalCustomerId from the authenticated JWT claims.
    /// Returns (tenantId, customerId, null) on success or (null, null, errorResult) on failure.
    /// Never reads tenantId or customerId from the request body or query string.
    /// </summary>
    private static (string? TenantId, Guid? CustomerId, IResult? Error) ResolveCustomerContext(HttpContext ctx)
    {
        // TenantId is already resolved by TenantResolutionMiddleware from the JWT tenant_id claim.
        var tenantCtx = ctx.RequestServices.GetRequiredService<ITenantContext>();
        var tenantId  = tenantCtx.TenantId;

        if (string.IsNullOrWhiteSpace(tenantId))
            return (null, null, Results.Problem(statusCode: 400, title: "Tenant context required"));

        var customerIdRaw = ctx.User.FindFirst(CustomerIdClaim)?.Value;
        if (!Guid.TryParse(customerIdRaw, out var customerId))
        {
            return (null, null, Results.Problem(
                statusCode: 403,
                title: "Customer identity missing",
                detail: $"JWT must contain a valid '{CustomerIdClaim}' claim."));
        }

        return (tenantId, customerId, null);
    }
}

/// <summary>
/// Minimal request body for a customer comment.
/// Author identity comes from JWT claims (email, name), not from the request body.
/// </summary>
public class CustomerAddCommentRequest
{
    public string Body { get; set; } = default!;
}
