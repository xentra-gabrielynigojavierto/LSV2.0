using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class SmsReconciliationEndpoints
{
    private const int MaxBatchLimit    = 200;
    private const int DefaultBatchSize = 50;
    private const int DefaultStaleMin  = 30;

    public static void MapSmsReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/sms/reconciliation").WithTags("SMS Reconciliation");

        // POST /v1/sms/reconciliation/attempts/{attemptId}
        // Reconcile a specific outbound SMS attempt by its local attempt ID.
        // Returns the reconciliation outcome including whether local status was updated.
        group.MapPost("/attempts/{attemptId:guid}", async (
            Guid attemptId,
            ISmsReconciliationService svc,
            CancellationToken ct) =>
        {
            var result = await svc.ReconcileByAttemptIdAsync(attemptId, ct);
            return result.Outcome == SmsReconciliationResult.OutcomeAttemptNotFound
                ? Results.NotFound(result)
                : Results.Ok(result);
        }).RequireAuthorization();

        // POST /v1/sms/reconciliation/provider-messages/{providerMessageId}
        // Reconcile a specific outbound SMS attempt by its Twilio MessageSid.
        // Useful when the local attempt ID is unknown but the provider SID is available.
        group.MapPost("/provider-messages/{providerMessageId}", async (
            string providerMessageId,
            ISmsReconciliationService svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(providerMessageId))
                return Results.BadRequest(new { error = "providerMessageId is required" });

            // Sanitize — provider message IDs are alphanumeric (Twilio SIDs start with SM)
            var cleaned = providerMessageId.Trim();
            if (cleaned.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[A-Za-z0-9_\-]+$"))
                return Results.BadRequest(new { error = "providerMessageId contains invalid characters" });

            var result = await svc.ReconcileByProviderMessageIdAsync(cleaned, ct);
            return result.Outcome == SmsReconciliationResult.OutcomeAttemptNotFound
                ? Results.NotFound(result)
                : Results.Ok(result);
        }).RequireAuthorization();

        // POST /v1/sms/reconciliation/stale?limit={n}&olderThanMinutes={n}
        // Batch reconcile stale/pending outbound SMS attempts.
        // Limit is capped at 200 to prevent excessive Twilio API calls in a single request.
        group.MapPost("/stale", async (
            ISmsReconciliationService svc,
            CancellationToken ct,
            int? limit,
            int? olderThanMinutes) =>
        {
            var batchLimit   = Math.Min(limit ?? DefaultBatchSize, MaxBatchLimit);
            var staleMinutes = Math.Max(olderThanMinutes ?? DefaultStaleMin, 5);
            var olderThan    = TimeSpan.FromMinutes(staleMinutes);

            var result = await svc.ReconcileStalePendingAsync(batchLimit, olderThan, ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
