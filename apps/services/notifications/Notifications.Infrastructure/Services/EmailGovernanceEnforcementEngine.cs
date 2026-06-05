using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Email governance enforcement engine.
///
/// Evaluates email rendered subject and body against governance rules resolved
/// by the topology graph. Uses GovernanceRuleEvaluationHelper for rule matching.
///
/// NEVER persists raw subject/body text, recipient email addresses, or full message content.
/// If body and subject are both unavailable: fails open with insufficient_context.
/// </summary>
public sealed class EmailGovernanceEnforcementEngine : IGovernanceChannelEnforcementEngine
{
    private readonly GovernanceRuleEvaluationHelper _helper;
    private readonly ILogger<EmailGovernanceEnforcementEngine> _logger;

    public string ChannelType         => "email";
    public bool   SupportsSimulation  => true;

    public EmailGovernanceEnforcementEngine(
        GovernanceRuleEvaluationHelper helper,
        ILogger<EmailGovernanceEnforcementEngine> logger)
    {
        _helper = helper;
        _logger = logger;
    }

    public async Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.PayloadTextForEvaluation) &&
            string.IsNullOrWhiteSpace(context.SubjectMetadata))
        {
            _logger.LogDebug("EmailGovernanceEnforcementEngine: no payload context for notification {Id} — fail open", context.NotificationId);
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
                subjectText:       context.SubjectMetadata,
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
            _logger.LogWarning(ex, "EmailGovernanceEnforcementEngine: evaluation error for notification {Id} — fail open", context.NotificationId);
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
        var start = DateTime.UtcNow;
        var context = new GovernanceExecutionContext
        {
            TenantId               = request.TenantId,
            ChannelType            = ChannelType,
            TemplateId             = request.TemplateId,
            TemplateKey            = request.TemplateKey,
            RolloutPlanId          = request.RolloutPlanId,
            ReleasePackageId       = request.ReleasePackageId,
            SubjectMetadata        = request.SubjectText,
            PayloadTextForEvaluation = request.SimulationPayloadText,  // transient
            EvaluationContext      = request.EvaluationContext ?? "simulation",
            ExecutedAtUtc          = DateTime.UtcNow
        };

        var result    = await EvaluateAsync(context, topology, ct);
        var duration  = DateTime.UtcNow - start;

        return new GovernanceSimulationResult
        {
            Execution          = result,
            RulesEvaluated     = topology.FinalRuleCount,
            EvaluationDuration = duration,
            SimulationWarnings = topology.Warnings.ToArray()
        };
    }

    private GovernanceExecutionResult BuildResult(
        string decisionType,
        string reasonCode,
        Guid? tenantId,
        IReadOnlyList<Guid> ruleIds,
        IReadOnlyList<Guid> packIds,
        string? classification,
        string engineStatus)
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
