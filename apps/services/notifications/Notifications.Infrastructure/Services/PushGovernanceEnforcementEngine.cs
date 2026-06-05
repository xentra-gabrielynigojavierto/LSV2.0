using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Push governance enforcement engine.
///
/// The Push channel is currently a reserved placeholder — no active push provider
/// implementation exists in the delivery pipeline. This engine provides:
/// - Runtime status visibility (channel registered, enforcement enabled)
/// - Simulation capability for future testing
/// - Safe fail-open behavior when push payload context is unavailable
///
/// When push payload (title/body) is provided in PayloadTextForEvaluation, applies
/// rule evaluation using GovernanceRuleEvaluationHelper.
/// When payload is absent: fails open with insufficient_context.
///
/// NEVER persists push title/body, device tokens, or recipient identifiers.
/// </summary>
public sealed class PushGovernanceEnforcementEngine : IGovernanceChannelEnforcementEngine
{
    private readonly GovernanceRuleEvaluationHelper _helper;
    private readonly ILogger<PushGovernanceEnforcementEngine> _logger;

    public string ChannelType        => "push";
    public bool   SupportsSimulation => true;

    public PushGovernanceEnforcementEngine(
        GovernanceRuleEvaluationHelper helper,
        ILogger<PushGovernanceEnforcementEngine> logger)
    {
        _helper = helper;
        _logger = logger;
    }

    public async Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        // Push channel is a reserved placeholder in the delivery pipeline.
        // When payload context is available, evaluate. Otherwise fail open.
        if (string.IsNullOrWhiteSpace(context.PayloadTextForEvaluation))
        {
            _logger.LogDebug("PushGovernanceEnforcementEngine: no push payload context — fail open (push pipeline reserved)");
            return BuildResult(
                GovernanceDecisionTypes.Allow,
                GovernanceReasonCodes.InsufficientContext,
                context.TenantId,
                Array.Empty<Guid>(),
                Array.Empty<Guid>(),
                null,
                engineStatus: "fail_open");
        }

        try
        {
            var evalResult = await _helper.EvaluateTextContentAsync(
                channelType:       ChannelType,
                tenantId:          context.TenantId,
                subjectText:       context.SubjectMetadata,   // push title if provided
                bodyText:          context.PayloadTextForEvaluation,
                safeMetadataHint:  context.PayloadMetadataJson,
                graph:             topology,
                evaluationContext: context.EvaluationContext,
                ct:                ct);

            return BuildResult(
                evalResult.DecisionType,
                evalResult.ReasonCode,
                context.TenantId,
                evalResult.MatchedRuleIds,
                evalResult.MatchedPackIds,
                evalResult.ContentClassification,
                engineStatus: "ok");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PushGovernanceEnforcementEngine: evaluation error — fail open");
            return BuildResult(
                GovernanceDecisionTypes.Allow,
                GovernanceReasonCodes.ChannelEngineFailed,
                context.TenantId,
                Array.Empty<Guid>(),
                Array.Empty<Guid>(),
                null,
                engineStatus: "fail_open");
        }
    }

    public async Task<GovernanceSimulationResult> SimulateAsync(
        GovernanceSimulationRequest request,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        var start   = DateTime.UtcNow;
        var context = new GovernanceExecutionContext
        {
            TenantId                 = request.TenantId,
            ChannelType              = ChannelType,
            TemplateId               = request.TemplateId,
            TemplateKey              = request.TemplateKey,
            SubjectMetadata          = request.SubjectText,
            PayloadTextForEvaluation = request.SimulationPayloadText,
            EvaluationContext        = request.EvaluationContext ?? "simulation",
            ExecutedAtUtc            = DateTime.UtcNow
        };

        var result   = await EvaluateAsync(context, topology, ct);
        var duration = DateTime.UtcNow - start;

        var warnings = new List<string>(topology.Warnings);
        warnings.Add("Push channel delivery pipeline is reserved — integration pending push provider implementation.");

        return new GovernanceSimulationResult
        {
            Execution          = result,
            RulesEvaluated     = topology.FinalRuleCount,
            EvaluationDuration = duration,
            SimulationWarnings = warnings
        };
    }

    private GovernanceExecutionResult BuildResult(
        string decisionType, string reasonCode, Guid? tenantId,
        IReadOnlyList<Guid> ruleIds, IReadOnlyList<Guid> packIds,
        string? classification, string engineStatus)
    {
        var shouldBlock  = decisionType == GovernanceDecisionTypes.Block;
        var shouldReview = decisionType == GovernanceDecisionTypes.ReviewRequired;
        var shouldWarn   = decisionType == GovernanceDecisionTypes.Warn;

        return new GovernanceExecutionResult
        {
            DecisionType          = decisionType,
            ReasonCode            = reasonCode,
            ChannelType           = ChannelType,
            TenantId              = tenantId,
            MatchedRuleIds        = ruleIds,
            MatchedRulePackIds    = packIds,
            AppliedOverlayIds     = Array.Empty<Guid>(),
            ContentClassification = classification,
            ShouldProceed         = !shouldBlock && !shouldReview,
            ShouldWarn            = shouldWarn,
            ShouldBlock           = shouldBlock,
            RequiresReview        = shouldReview,
            EngineStatus          = engineStatus,
        };
    }
}
