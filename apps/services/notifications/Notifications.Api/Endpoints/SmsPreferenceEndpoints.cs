using Notifications.Api.Middleware;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class SmsPreferenceEndpoints
{
    public static void MapSmsPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/sms/preferences").WithTags("SMS Preferences");

        // GET /v1/sms/preferences?phone=+15551234567
        // Returns the current SMS preference state for a phone number within the tenant.
        group.MapGet("", async (HttpContext context, ISmsPreferenceService svc, string phone) =>
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Results.BadRequest(new { error = "phone query parameter is required" });

            var tenantId = context.GetTenantId();
            var state    = await svc.GetPreferenceStateAsync(tenantId, phone);
            return Results.Ok(new { phone, preferenceState = state });
        }).RequireAuthorization();

        // GET /v1/sms/preferences/list?limit=50&offset=0
        // Lists all SMS preferences for the tenant (operator/admin use).
        group.MapGet("/list", async (HttpContext context, ISmsPreferenceService svc, int? limit, int? offset) =>
        {
            var tenantId = context.GetTenantId();
            var items    = await svc.ListAsync(tenantId, limit ?? 50, offset ?? 0);
            return Results.Ok(items);
        }).RequireAuthorization();

        // PUT /v1/sms/preferences
        // Manually set the SMS preference for a phone number. Audited. Writes history.
        group.MapPut("", async (HttpContext context, ISmsPreferenceService svc, SetSmsPreferenceDto request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
                return Results.BadRequest(new { error = "phone is required" });

            if (request.PreferenceState is not ("opted_in" or "opted_out"))
                return Results.BadRequest(new { error = "preferenceState must be 'opted_in' or 'opted_out'" });

            var tenantId    = context.GetTenantId();
            var actorUserId = context.User.FindFirst("sub")?.Value;

            try
            {
                var result = await svc.SetPreferenceAsync(tenantId, request.Phone, request.PreferenceState, request.Reason, actorUserId);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        // GET /v1/sms/preferences/history?phone=+15551234567&limit=50&offset=0
        // Returns immutable preference change history for a phone number within the tenant.
        // History is append-only — records are never updated or deleted.
        group.MapGet("/history", async (HttpContext context, ISmsPreferenceService svc, string phone, int? limit, int? offset) =>
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Results.BadRequest(new { error = "phone query parameter is required" });

            var tenantId = context.GetTenantId();
            var result   = await svc.GetHistoryAsync(tenantId, phone, limit ?? 50, offset ?? 0);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
