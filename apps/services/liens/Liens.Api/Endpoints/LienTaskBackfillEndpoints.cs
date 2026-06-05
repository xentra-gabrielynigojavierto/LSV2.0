using Liens.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Liens.Api.Endpoints;

/// <summary>
/// TASK-B04 — one-shot admin endpoint to backfill all existing Liens task rows into
/// the canonical Task service.
///
/// POST /api/liens/internal/task-backfill
///   Header: X-Internal-Service-Token = {FLOW_SERVICE_TOKEN_SECRET}
///   Body:   { "batchSize": 100, "actingAdminUserId": "..." }
///
/// Protected by the same shared-secret scheme as FlowEventsEndpoints.
/// Idempotent — safe to run multiple times.
/// </summary>
public static class LienTaskBackfillEndpoints
{
    private const string InternalTokenHeader = "X-Internal-Service-Token";

    public static IEndpointRouteBuilder MapLienTaskBackfillEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/liens/internal/task-backfill", HandleBackfill)
            .AllowAnonymous();

        return routes;
    }

    private static async Task<IResult> HandleBackfill(
        HttpContext                httpContext,
        BackfillRequest?           body,
        ILienTaskBackfillService   backfillService,
        IConfiguration             config,
        ILoggerFactory             loggerFactory,
        CancellationToken          ct)
    {
        var logger = loggerFactory.CreateLogger("LienTaskBackfillEndpoints");

        var expectedToken = config["FLOW_SERVICE_TOKEN_SECRET"]
                         ?? Environment.GetEnvironmentVariable("FLOW_SERVICE_TOKEN_SECRET");
        var suppliedToken = httpContext.Request.Headers[InternalTokenHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(suppliedToken)
            || suppliedToken != expectedToken)
        {
            logger.LogWarning("LienTaskBackfillEndpoints: Rejected request — invalid or missing {Header}.",
                InternalTokenHeader);
            return Results.Unauthorized();
        }

        if (body is null)
            return Results.BadRequest(new { error = "Request body is required." });

        if (body.ActingAdminUserId == Guid.Empty)
            return Results.BadRequest(new { error = "actingAdminUserId is required." });

        var batchSize = body.BatchSize is > 0 and <= 500 ? body.BatchSize : 100;

        logger.LogInformation(
            "LienTaskBackfillEndpoints: Starting backfill, adminUser={AdminUser} batchSize={BatchSize}",
            body.ActingAdminUserId, batchSize);

        var report = await backfillService.RunAsync(body.ActingAdminUserId, batchSize, ct);

        return Results.Ok(new
        {
            attempted    = report.Attempted,
            created      = report.Created,
            alreadyExisted = report.AlreadyExisted,
            failed       = report.Failed,
            totalNotes   = report.TotalNotes,
            totalLinks   = report.TotalLinks,
            elapsedMs    = (long)report.Elapsed.TotalMilliseconds,
        });
    }

    private sealed class BackfillRequest
    {
        public Guid ActingAdminUserId { get; init; }
        public int  BatchSize         { get; init; } = 100;
    }
}
