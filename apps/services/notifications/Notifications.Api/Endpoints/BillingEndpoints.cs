using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Api.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/billing").WithTags("Billing");

        group.MapGet("/plan", async (HttpContext context, ITenantBillingPlanRepository repo) =>
        {
            var tenantId = context.GetTenantId();
            var plan = await repo.FindActivePlanAsync(tenantId);
            return plan != null ? Results.Ok(MapPlanDto(plan)) : Results.NotFound();
        });

        group.MapGet("/plans", async (HttpContext context, ITenantBillingPlanRepository repo) =>
        {
            var tenantId = context.GetTenantId();
            var plans = await repo.GetByTenantAsync(tenantId);
            return Results.Ok(plans.Select(MapPlanDto));
        });

        group.MapGet("/rates/{planId:guid}", async (HttpContext context, ITenantBillingRateRepository repo, ITenantBillingPlanRepository planRepo, Guid planId) =>
        {
            var tenantId = context.GetTenantId();
            var plan = await planRepo.GetByIdAsync(planId);
            if (plan == null || plan.TenantId != tenantId) return Results.NotFound();
            var rates = await repo.GetByPlanIdAsync(planId);
            return Results.Ok(rates.Select(MapRateDto));
        });

        group.MapGet("/rate-limits", async (HttpContext context, ITenantRateLimitPolicyRepository repo) =>
        {
            var tenantId = context.GetTenantId();
            var policies = await repo.GetByTenantAsync(tenantId);
            return Results.Ok(policies);
        });
    }

    private static BillingPlanDto MapPlanDto(TenantBillingPlan p) => new()
    {
        Id = p.Id, TenantId = p.TenantId, PlanName = p.PlanName, BillingMode = p.BillingMode,
        Status = p.Status, MonthlyFlatRate = p.MonthlyFlatRate, Currency = p.Currency,
        EffectiveFrom = p.EffectiveFrom, EffectiveTo = p.EffectiveTo,
        CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
    };

    private static BillingRateDto MapRateDto(TenantBillingRate r) => new()
    {
        Id = r.Id, BillingPlanId = r.BillingPlanId, UsageUnit = r.UsageUnit,
        Channel = r.Channel, ProviderOwnershipMode = r.ProviderOwnershipMode,
        UnitPrice = r.UnitPrice, Currency = r.Currency, IsBillable = r.IsBillable,
        CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
    };
}
