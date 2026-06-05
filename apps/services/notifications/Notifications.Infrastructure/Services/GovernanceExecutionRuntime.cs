using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Unified governance execution runtime.
///
/// Orchestrates cross-channel governance enforcement:
/// 1. Validate and normalize channel type.
/// 2. Resolve topology using IGovernanceTopologyResolver.
/// 3. Select registered IGovernanceChannelEnforcementEngine for the channel.
/// 4. Execute channel-specific enforcement against effective rules.
/// 5. Persist safe telemetry via IGovernanceExecutionTelemetryService (non-fatal).
/// 6. Return decision result.
/// 7. Fail open when FailOpenOnRuntimeError = true.
///
/// Critical:
/// - Does NOT alter existing SMS governance path.
/// - Does NOT call external services or provider APIs.
/// - Does NOT persist raw payloads, phone numbers, email addresses, or credentials.
/// - Engine failures are isolated — one channel failing does not affect others.
/// </summary>
public sealed class GovernanceExecutionRuntime : IGovernanceExecutionRuntime
{
    private readonly IReadOnlyDictionary<string, IGovernanceChannelEnforcementEngine> _engines;
    private readonly IGovernanceTopologyResolver _topologyResolver;
    private readonly IGovernanceExecutionTelemetryService _telemetry;
    private readonly IOptions<GovernanceExecutionRuntimeOptions> _options;
    private readonly ILogger<GovernanceExecutionRuntime> _logger;

    public GovernanceExecutionRuntime(
        IEnumerable<IGovernanceChannelEnforcementEngine> engines,
        IGovernanceTopologyResolver topologyResolver,
        IGovernanceExecutionTelemetryService telemetry,
        IOptions<GovernanceExecutionRuntimeOptions> options,
        ILogger<GovernanceExecutionRuntime> logger)
    {
        _engines          = engines.ToDictionary(e => e.ChannelType, StringComparer.OrdinalIgnoreCase);
        _topologyResolver = topologyResolver;
        _telemetry        = telemetry;
        _options          = options;
        _logger           = logger;
    }

    public async Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        CancellationToken ct = default)
    {
        var opts = _options.Value;

        if (!opts.Enabled)
            return FailOpen(context.ChannelType, context.TenantId, GovernanceReasonCodes.NoApplicableRules, "disabled");

        // Normalize channel type
        var channel = NormalizeChannel(context.ChannelType);

        // Check per-channel enforcement flags
        if (!IsEnforcementEnabled(channel, opts))
        {
            _logger.LogDebug("GovernanceExecutionRuntime: enforcement disabled for channel {Channel}", channel);
            return FailOpen(channel, context.TenantId, GovernanceReasonCodes.NoApplicableRules, "disabled");
        }

        // Step 1: Resolve topology
        GovernanceTopologyGraph topology;
        string topologyStatus;
        try
        {
            var topoRequest = new GovernanceTopologyRequest(
                TenantId:        context.TenantId,
                ChannelType:     channel,
                RolloutPlanId:   context.RolloutPlanId,
                ReleasePackageId: context.ReleasePackageId,
                EvaluationContext: context.EvaluationContext,
                NowUtc:          context.ExecutedAtUtc);

            topology      = await _topologyResolver.ResolveTopologyAsync(topoRequest, ct);
            topologyStatus = "ok";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GovernanceExecutionRuntime: topology resolution failed for channel {Channel} — fail open={FailOpen}",
                channel, opts.FailOpenOnRuntimeError);

            topologyStatus = "error";
            if (opts.FailOpenOnRuntimeError)
            {
                var fo = FailOpen(channel, context.TenantId, GovernanceReasonCodes.TopologyResolutionFailed, "fail_open");
                fo.TopologyResolutionStatus = topologyStatus;
                await TryRecordAsync(context, fo, false, ct);
                return fo;
            }

            return new GovernanceExecutionResult
            {
                DecisionType            = GovernanceDecisionTypes.Block,
                ReasonCode              = GovernanceReasonCodes.TopologyResolutionFailed,
                ChannelType             = channel,
                TenantId                = context.TenantId,
                ShouldProceed           = false,
                ShouldBlock             = true,
                TopologyResolutionStatus = topologyStatus,
                EngineStatus            = "topology_failed",
            };
        }

        // Step 2: Select engine
        if (!_engines.TryGetValue(channel, out var engine))
        {
            _logger.LogDebug("GovernanceExecutionRuntime: no engine registered for channel {Channel} — fail open", channel);
            var fo = FailOpen(channel, context.TenantId, GovernanceReasonCodes.NoEngineRegistered, "no_engine");
            fo.TopologyResolutionStatus = topologyStatus;
            await TryRecordAsync(context, fo, false, ct);
            return fo;
        }

        // Step 3: Execute engine
        GovernanceExecutionResult result;
        try
        {
            result = await engine.EvaluateAsync(context, topology, ct);
            result.TopologyResolutionStatus = topologyStatus;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GovernanceExecutionRuntime: engine {Channel} threw unexpectedly — fail open={FailOpen}",
                channel, opts.FailOpenOnRuntimeError);

            if (opts.FailOpenOnRuntimeError)
            {
                var fo = FailOpen(channel, context.TenantId, GovernanceReasonCodes.ChannelEngineFailed, "fail_open");
                fo.TopologyResolutionStatus = topologyStatus;
                await TryRecordAsync(context, fo, false, ct);
                return fo;
            }

            result = new GovernanceExecutionResult
            {
                DecisionType            = GovernanceDecisionTypes.Block,
                ReasonCode              = GovernanceReasonCodes.ChannelEngineFailed,
                ChannelType             = channel,
                TenantId                = context.TenantId,
                ShouldProceed           = false,
                ShouldBlock             = true,
                TopologyResolutionStatus = topologyStatus,
                EngineStatus            = "error",
            };
        }

        // Step 4: Persist telemetry (non-fatal)
        await TryRecordAsync(context, result, false, ct);

        return result;
    }

    public async Task<GovernanceSimulationResult> SimulateAsync(
        GovernanceSimulationRequest request,
        CancellationToken ct = default)
    {
        var opts    = _options.Value;
        var channel = NormalizeChannel(request.ChannelType);
        var start   = DateTime.UtcNow;

        // Resolve topology for simulation
        GovernanceTopologyGraph topology;
        TopologyExplanation? explanation = null;
        var warnings = new List<string>();

        try
        {
            var topoRequest = new GovernanceTopologyRequest(
                TenantId:        request.TenantId,
                ChannelType:     channel,
                RolloutPlanId:   request.RolloutPlanId,
                ReleasePackageId: request.ReleasePackageId,
                EvaluationContext: request.EvaluationContext ?? "simulation",
                NowUtc:          DateTime.UtcNow);

            topology    = await _topologyResolver.ResolveTopologyAsync(topoRequest, ct);
            explanation = await _topologyResolver.ExplainTopologyAsync(topoRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GovernanceExecutionRuntime.SimulateAsync: topology resolution failed for channel {Channel}", channel);
            warnings.Add($"Topology resolution failed: {ex.Message}");

            var emptyGraph = new GovernanceTopologyGraph(
                channel, request.TenantId, "unknown",
                Array.Empty<ChannelPackSummary>(), Array.Empty<ChannelPackSummary>(),
                Array.Empty<ChannelPackSummary>(), Array.Empty<ChannelPackSummary>(),
                Array.Empty<FederationOverlaySummary>(), Array.Empty<FederationOverlaySummary>(),
                Array.Empty<string>(), 0, new[] { "Topology resolution failed" });

            topology = emptyGraph;
        }

        // Select engine
        GovernanceSimulationResult simResult;
        if (!_engines.TryGetValue(channel, out var engine))
        {
            warnings.Add($"No governance engine registered for channel '{channel}'.");
            simResult = new GovernanceSimulationResult
            {
                Execution = FailOpen(channel, request.TenantId, GovernanceReasonCodes.NoEngineRegistered, "no_engine"),
                RulesEvaluated = topology.FinalRuleCount,
                EvaluationDuration = DateTime.UtcNow - start,
                SimulationWarnings = warnings
            };
        }
        else if (!engine.SupportsSimulation)
        {
            warnings.Add($"Engine for channel '{channel}' does not support simulation.");
            simResult = new GovernanceSimulationResult
            {
                Execution = FailOpen(channel, request.TenantId, GovernanceReasonCodes.InsufficientContext, "no_simulation_support"),
                RulesEvaluated = topology.FinalRuleCount,
                EvaluationDuration = DateTime.UtcNow - start,
                SimulationWarnings = warnings
            };
        }
        else
        {
            try
            {
                simResult = await engine.SimulateAsync(request, topology, ct);
                simResult.Explanation = explanation;
                // Merge any additional warnings
                var merged = new List<string>(simResult.SimulationWarnings);
                merged.AddRange(warnings);
                simResult.SimulationWarnings = merged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GovernanceExecutionRuntime.SimulateAsync: engine threw for channel {Channel}", channel);
                simResult = new GovernanceSimulationResult
                {
                    Execution = FailOpen(channel, request.TenantId, GovernanceReasonCodes.ChannelEngineFailed, "fail_open"),
                    RulesEvaluated = topology.FinalRuleCount,
                    EvaluationDuration = DateTime.UtcNow - start,
                    SimulationWarnings = new[] { $"Engine error: {ex.Message}" }
                };
            }
        }

        // Persist simulation telemetry record (no raw payload persisted)
        var simContext = new GovernanceExecutionContext
        {
            TenantId         = request.TenantId,
            ChannelType      = channel,
            TemplateId       = request.TemplateId,
            TemplateKey      = request.TemplateKey,
            RolloutPlanId    = request.RolloutPlanId,
            ReleasePackageId = request.ReleasePackageId,
            EvaluationContext = request.EvaluationContext ?? "simulation",
            ExecutedAtUtc    = start,
            // PayloadTextForEvaluation intentionally NOT set here — transient only
        };
        await TryRecordAsync(simContext, simResult.Execution, true, ct);

        return simResult;
    }

    public Task<IReadOnlyList<GovernanceChannelRuntimeStatus>> GetChannelRuntimeStatusAsync(
        CancellationToken ct = default)
    {
        var opts = _options.Value;

        var allChannels = new[] { "email", "push", "webhook", "sms", "in-app" };
        var result = new List<GovernanceChannelRuntimeStatus>();

        foreach (var channel in allChannels)
        {
            _engines.TryGetValue(channel, out var engine);

            result.Add(new GovernanceChannelRuntimeStatus
            {
                ChannelType        = channel,
                EngineRegistered   = engine != null,
                SupportsSimulation = engine?.SupportsSimulation ?? false,
                EnforcementEnabled = opts.Enabled && IsEnforcementEnabled(channel, opts),
                EnforcementMode    = GetEnforcementMode(channel, engine, opts),
                Notes              = GetChannelNotes(channel, opts),
            });
        }

        // Add any registered engines not in the default list
        foreach (var kvp in _engines)
        {
            if (!allChannels.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(new GovernanceChannelRuntimeStatus
                {
                    ChannelType        = kvp.Key,
                    EngineRegistered   = true,
                    SupportsSimulation = kvp.Value.SupportsSimulation,
                    EnforcementEnabled = opts.Enabled,
                    EnforcementMode    = "active",
                });
            }
        }

        return Task.FromResult<IReadOnlyList<GovernanceChannelRuntimeStatus>>(result);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string NormalizeChannel(string? channel) =>
        (channel ?? string.Empty).ToLowerInvariant().Trim();

    private static bool IsEnforcementEnabled(string channel, GovernanceExecutionRuntimeOptions opts) =>
        channel switch
        {
            "email"   => opts.EnableEmailEnforcement,
            "push"    => opts.EnablePushEnforcement,
            "webhook" => opts.EnableWebhookEnforcement,
            "sms"     => opts.EnableSmsCompatibilityRuntime,
            _         => true,
        };

    private static string GetEnforcementMode(
        string channel,
        IGovernanceChannelEnforcementEngine? engine,
        GovernanceExecutionRuntimeOptions opts) =>
        engine == null ? "no_engine"
        : !IsEnforcementEnabled(channel, opts) ? "disabled"
        : channel == "sms" ? "compatibility_passthrough"
        : channel is "push" or "webhook" ? "active_pipeline_pending"
        : "active";

    private static string? GetChannelNotes(string channel, GovernanceExecutionRuntimeOptions opts) =>
        channel switch
        {
            "sms" when !opts.EnableSmsCompatibilityRuntime =>
                "SMS governance runs through LS-017–023 pipeline. Compatibility runtime disabled.",
            "sms" =>
                "SMS governance active via LS-017–023 pipeline. Compatibility runtime provides simulation/status only.",
            "push" =>
                "Push delivery pipeline is reserved — governance engine active for simulation; live enforcement pending push provider.",
            "webhook" =>
                "General webhook pipeline is reserved — governance engine active for simulation; live enforcement pending webhook delivery implementation.",
            _ => null
        };

    private static GovernanceExecutionResult FailOpen(
        string channel, Guid? tenantId, string reasonCode, string engineStatus) =>
        new()
        {
            DecisionType   = GovernanceDecisionTypes.Allow,
            ReasonCode     = reasonCode,
            ChannelType    = channel,
            TenantId       = tenantId,
            ShouldProceed  = true,
            EngineStatus   = engineStatus,
        };

    private async Task TryRecordAsync(
        GovernanceExecutionContext context,
        GovernanceExecutionResult result,
        bool isSimulation,
        CancellationToken ct)
    {
        try
        {
            await _telemetry.RecordExecutionAsync(context, result, isSimulation, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GovernanceExecutionRuntime: telemetry persistence failed — non-fatal");
        }
    }
}
