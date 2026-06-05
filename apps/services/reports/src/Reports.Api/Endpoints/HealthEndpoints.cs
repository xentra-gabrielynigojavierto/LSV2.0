using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Adapters;
using Reports.Contracts.Audit;
using Reports.Contracts.Context;
using Reports.Contracts.Guardrails;
using Reports.Contracts.Queue;
using Reports.Infrastructure.Persistence;

namespace Reports.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1")
            .WithTags("Health")
            .AllowAnonymous();

        group.MapGet("/health", () => Results.Ok(new
        {
            status    = "healthy",
            service   = "Reports Service",
            timestamp = DateTimeOffset.UtcNow,
        }));

        group.MapGet("/ready", async (
            IServiceProvider sp,
            IIdentityAdapter identity,
            ITenantAdapter tenant,
            IEntitlementAdapter entitlement,
            IAuditAdapter audit,
            IDocumentAdapter document,
            INotificationAdapter notification,
            IProductDataAdapter productData,
            IJobQueue queue,
            IGuardrailValidator guardrails,
            IConfiguration config) =>
        {
            var ctx = RequestContext.Default();
            var mockTenant = new TenantContext { TenantId = "probe", IsActive = true };
            var mockUser = new UserContext { UserId = "probe" };
            var mockNotification = new ReportNotification { ReportId = "probe", ReportName = "probe" };

            var checks = new Dictionary<string, string>();

            checks["config_loaded"] = !string.IsNullOrEmpty(config["ReportsService:ServiceName"]) ? "ok" : "fail";

            var dbContext = sp.GetService<ReportsDbContext>();
            if (dbContext is not null)
            {
                try
                {
                    await dbContext.Database.CanConnectAsync();
                    checks["database"] = "ok";
                }
                catch
                {
                    checks["database"] = "fail";
                }
            }
            else
            {
                checks["database"] = "mock";
            }

            checks["identity_adapter"] = (await ProbeAdapter(() => identity.ValidateTokenAsync(ctx, "probe"))).Success ? "ok" : "fail";
            checks["tenant_adapter"] = (await ProbeAdapter(() => tenant.IsTenantActiveAsync(ctx, "probe"))).Success ? "ok" : "fail";
            checks["entitlement_adapter"] = (await ProbeAdapter(() => entitlement.CanAccessReportsAsync(ctx, mockTenant, mockUser))).Success ? "ok" : "fail";

            if (audit.IsRealIntegration)
            {
                var probeEvent = new AuditEventDto
                {
                    EventType = "readiness.probe",
                    TenantId = "probe",
                    ActorUserId = "probe",
                    EntityType = "HealthCheck",
                    EntityId = "probe",
                    Action = "readiness",
                    Description = "probe"
                };
                var auditProbe = await ProbeAdapter(() => audit.RecordEventAsync(probeEvent));
                checks["audit_adapter"] = auditProbe.Success ? "ok" : "fail";
            }
            else
            {
                checks["audit_adapter"] = "mock";
            }

            var docProbe = await ProbeAdapter(() => document.RetrieveReportAsync(ctx, mockTenant, "probe"));
            checks["document_adapter"] = docProbe.Called && (docProbe.Success || docProbe.ErrorCode == AdapterErrors.NotFound) ? "ok" : "fail";
            checks["notification_adapter"] = (await ProbeAdapter(() => notification.NotifyReportReadyAsync(ctx, mockTenant, "probe", mockNotification))).Success ? "ok" : "fail";
            checks["product_data_adapter"] = (await ProbeAdapter(() => productData.GetAvailableProductsAsync(ctx, mockTenant))).Success ? "ok" : "fail";
            checks["job_queue"] = await ProbeAdapterSimple(() => queue.GetPendingCountAsync()) ? "ok" : "fail";
            checks["guardrails"] = guardrails.ValidateExecutionLimits("probe", "probe").IsValid
                && guardrails.ValidateReportTemplate("probe").IsValid ? "ok" : "fail";

            var allOk = checks.Values.All(v => v == "ok" || v == "mock");

            var response = new
            {
                status    = allOk ? "ready" : "degraded",
                service   = "Reports Service",
                checks,
                timestamp = DateTimeOffset.UtcNow,
            };

            return allOk ? Results.Ok(response) : Results.Json(response, statusCode: 503);
        });
    }

    private static async Task<(bool Success, bool Called, string? ErrorCode)> ProbeAdapter<T>(Func<Task<AdapterResult<T>>> probe)
    {
        try
        {
            var result = await probe();
            return (result.Success, true, result.ErrorCode);
        }
        catch
        {
            return (false, false, null);
        }
    }

    private static async Task<bool> ProbeAdapterSimple<T>(Func<Task<T>> probe)
    {
        try { await probe(); return true; }
        catch { return false; }
    }
}
