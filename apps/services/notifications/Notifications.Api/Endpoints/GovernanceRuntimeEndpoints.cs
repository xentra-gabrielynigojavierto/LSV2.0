using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using BuildingBlocks.Authorization;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-025: Governance execution runtime admin endpoints.
///
/// Base: /notifications/v1/admin/governance/runtime
/// Auth: AdminOnly
///
/// 5 endpoints:
/// - GET  /status        — runtime status + engine health
/// - GET  /channels      — per-channel runtime status
/// - GET  /executions    — paginated execution telemetry records
/// - GET  /telemetry     — aggregate telemetry dashboard
/// - POST /simulate      — transient governance simulation (no raw payload persistence)
/// </summary>
public static class GovernanceRuntimeEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/notifications/v1/admin/governance/runtime")
            .RequireAuthorization(Policies.AdminOnly);

        // GET /status — overall runtime health
        grp.MapGet("/status", async (
            IGovernanceExecutionRuntime runtime,
            IOptions<GovernanceExecutionRuntimeOptions> options,
            CancellationToken ct) =>
        {
            var opts      = options.Value;
            var channels  = await runtime.GetChannelRuntimeStatusAsync(ct);

            return Results.Ok(new
            {
                enabled            = opts.Enabled,
                failOpenOnError    = opts.FailOpenOnRuntimeError,
                persistAllowDecisions = opts.PersistAllowDecisions,
                maxEvaluationTextLength = opts.MaxEvaluationTextLength,
                regexTimeoutMs     = opts.RegexTimeoutMs,
                registeredEngines  = channels.Count(c => c.EngineRegistered),
                enforcedChannels   = channels.Count(c => c.EnforcementEnabled),
                channelSummary     = channels.Select(c => new
                {
                    c.ChannelType,
                    c.EngineRegistered,
                    c.EnforcementEnabled,
                    c.EnforcementMode,
                    c.SupportsSimulation,
                    c.Notes
                })
            });
        });

        // GET /channels — per-channel runtime status detail
        grp.MapGet("/channels", async (
            IGovernanceExecutionRuntime runtime,
            CancellationToken ct) =>
        {
            var channels = await runtime.GetChannelRuntimeStatusAsync(ct);
            return Results.Ok(new { channels });
        });

        // GET /executions — paginated execution telemetry records
        grp.MapGet("/executions", async (
            IGovernanceExecutionTelemetryService telemetry,
            string? channelType,
            Guid? tenantId,
            string? decisionType,
            bool? isSimulation,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var query = new GovernanceExecutionQuery
            {
                ChannelType  = channelType,
                TenantId     = tenantId,
                DecisionType = decisionType,
                IsSimulation = isSimulation,
                From         = from,
                To           = to,
                Page         = Math.Max(1, page),
                PageSize     = Math.Clamp(pageSize, 1, 200)
            };

            var result = await telemetry.QueryExecutionsAsync(query, ct);
            return Results.Ok(result);
        });

        // GET /telemetry — aggregate runtime telemetry
        grp.MapGet("/telemetry", async (
            IGovernanceExecutionTelemetryService telemetry,
            string? channelType,
            Guid? tenantId,
            bool? isSimulation,
            DateTime? from,
            DateTime? to,
            CancellationToken ct = default) =>
        {
            var query = new GovernanceRuntimeTelemetryQuery
            {
                ChannelType  = channelType,
                TenantId     = tenantId,
                IsSimulation = isSimulation,
                From         = from,
                To           = to
            };

            var result = await telemetry.GetRuntimeTelemetryAsync(query, ct);
            return Results.Ok(result);
        });

        // POST /simulate — transient governance simulation
        // Raw payload text (simulationPayloadText) is evaluated in-memory and NEVER persisted.
        grp.MapPost("/simulate", async (
            SimulateGovernanceRequest body,
            IGovernanceExecutionRuntime runtime,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.ChannelType))
                return Results.BadRequest(new { error = "channelType is required" });

            var request = new GovernanceSimulationRequest
            {
                ChannelType          = body.ChannelType,
                TenantId             = body.TenantId,
                TemplateId           = body.TemplateId,
                TemplateKey          = body.TemplateKey,
                RolloutPlanId        = body.RolloutPlanId,
                ReleasePackageId     = body.ReleasePackageId,
                SimulationPayloadText = body.SimulationPayloadText,  // transient
                SubjectText          = body.SubjectText,
                EvaluationContext    = body.EvaluationContext ?? "api_simulation"
            };

            var result = await runtime.SimulateAsync(request, ct);

            // Return simulation result — no raw payload in response
            return Results.Ok(new
            {
                channelType        = result.Execution.ChannelType,
                tenantId           = result.Execution.TenantId,
                decisionType       = result.Execution.DecisionType,
                reasonCode         = result.Execution.ReasonCode,
                shouldProceed      = result.Execution.ShouldProceed,
                shouldWarn         = result.Execution.ShouldWarn,
                shouldBlock        = result.Execution.ShouldBlock,
                requiresReview     = result.Execution.RequiresReview,
                contentClassification = result.Execution.ContentClassification,
                matchedRuleIds     = result.Execution.MatchedRuleIds,
                matchedRulePackIds = result.Execution.MatchedRulePackIds,
                topologyResolutionStatus = result.Execution.TopologyResolutionStatus,
                engineStatus       = result.Execution.EngineStatus,
                rulesEvaluated     = result.RulesEvaluated,
                evaluationDurationMs = (int)result.EvaluationDuration.TotalMilliseconds,
                simulationWarnings = result.SimulationWarnings,
                explanation        = result.Explanation == null ? null : new
                {
                    channelType    = result.Explanation.ChannelType,
                    resolutionMode = result.Explanation.ResolutionMode,
                    federationEnabled = result.Explanation.FederationEnabled,
                    channelScopeFound = result.Explanation.ChannelScopeFound,
                    totalFinalRules = result.Explanation.TotalFinalRules,
                    steps          = result.Explanation.Steps.Select(s => new
                    {
                        s.StepNumber,
                        s.StepName,
                        s.Description,
                        s.RulesContributed,
                        s.RulesFiltered,
                        s.Details
                    })
                }
            });
        });

        return app;
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public sealed record SimulateGovernanceRequest(
    string  ChannelType,
    Guid?   TenantId,
    Guid?   TemplateId,
    string? TemplateKey,
    Guid?   RolloutPlanId,
    Guid?   ReleasePackageId,
    /// <summary>Transient only — evaluated in-memory, never persisted.</summary>
    string? SimulationPayloadText,
    string? SubjectText,
    string? EvaluationContext);
