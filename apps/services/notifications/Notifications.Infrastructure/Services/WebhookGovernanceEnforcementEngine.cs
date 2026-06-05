using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Webhook governance enforcement engine.
///
/// The general webhook channel is a reserved placeholder in the delivery pipeline.
/// Alert-escalation webhooks (Teams/Slack) use a specialized separate pipeline.
///
/// This engine evaluates ONLY safe payload metadata (event type, target category,
/// template key) — never raw JSON payloads, webhook URLs, or credentials.
///
/// When safe metadata is absent: fails open with insufficient_context.
/// When safe metadata is available: applies rule evaluation against metadata text.
///
/// NEVER persists raw webhook payloads, webhook URLs, auth tokens, or credentials.
/// </summary>
public sealed class WebhookGovernanceEnforcementEngine : IGovernanceChannelEnforcementEngine
{
    private readonly GovernanceRuleEvaluationHelper _helper;
    private readonly ILogger<WebhookGovernanceEnforcementEngine> _logger;

    public string ChannelType        => "webhook";
    public bool   SupportsSimulation => true;

    public WebhookGovernanceEnforcementEngine(
        GovernanceRuleEvaluationHelper helper,
        ILogger<WebhookGovernanceEnforcementEngine> logger)
    {
        _helper = helper;
        _logger = logger;
    }

    public async Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        // For webhook: use only safe metadata (event type / template key / category).
        // Raw JSON payload (PayloadTextForEvaluation) is accepted if already sanitized by caller.
        // If no safe metadata exists at all, fail open.
        var hasSafeContext =
            !string.IsNullOrWhiteSpace(context.PayloadMetadataJson) ||
            !string.IsNullOrWhiteSpace(context.TemplateKey) ||
            !string.IsNullOrWhiteSpace(context.EvaluationContext) ||
            !string.IsNullOrWhiteSpace(context.PayloadTextForEvaluation);

        if (!hasSafeContext)
        {
            _logger.LogDebug("WebhookGovernanceEnforcementEngine: no safe metadata context — fail open (webhook pipeline reserved)");
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
            // Build safe evaluation text from metadata + template key (no raw payload unless caller provided it as safe)
            var safeEvalText = string.Join(" ",
                context.TemplateKey ?? string.Empty,
                context.EvaluationContext ?? string.Empty,
                context.PayloadTextForEvaluation ?? string.Empty).Trim();

            var evalResult = await _helper.EvaluateTextContentAsync(
                channelType:       ChannelType,
                tenantId:          context.TenantId,
                subjectText:       context.SubjectMetadata,
                bodyText:          safeEvalText,
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
            _logger.LogWarning(ex, "WebhookGovernanceEnforcementEngine: evaluation error — fail open");
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
        warnings.Add("Webhook channel general delivery pipeline is reserved — raw payload never evaluated. Only safe metadata text used.");

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
