using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: SMS governance compatibility adapter.
///
/// This engine provides runtime status visibility and simulation for the SMS channel
/// WITHOUT replacing or duplicating existing SMS governance execution
/// (LS-017 through LS-023: SmsGovernancePolicyService, SmsTemplateGovernanceService,
/// SmsGovernanceRuleEngine).
///
/// Purpose:
/// - Show SMS channel as "active/enforced" in governance runtime status
/// - Support simulation via GovernanceRuleEvaluationHelper (no duplicate persistence)
/// - Does NOT intercept live SMS sends — SMS governance runs through its own pipeline
///
/// Enabled only when GovernanceExecutionRuntime:EnableSmsCompatibilityRuntime = true (default false).
/// </summary>
public sealed class SmsGovernanceCompatibilityEngine : IGovernanceChannelEnforcementEngine
{
    private readonly GovernanceRuleEvaluationHelper _helper;
    private readonly ILogger<SmsGovernanceCompatibilityEngine> _logger;

    public string ChannelType        => "sms";
    public bool   SupportsSimulation => true;

    public SmsGovernanceCompatibilityEngine(
        GovernanceRuleEvaluationHelper helper,
        ILogger<SmsGovernanceCompatibilityEngine> logger)
    {
        _helper = helper;
        _logger = logger;
    }

    /// <summary>
    /// SMS live evaluation is handled entirely by existing LS-017–023 governance pipeline.
    /// This adapter returns allow with sms_enforced reason — no duplicate decision persistence.
    /// </summary>
    public Task<GovernanceExecutionResult> EvaluateAsync(
        GovernanceExecutionContext context,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        // SMS governance already runs through SmsGovernancePolicyService + SmsTemplateGovernanceService.
        // Compatibility engine does NOT re-run evaluation to avoid duplicate decisions.
        _logger.LogDebug("SmsGovernanceCompatibilityEngine: SMS channel live eval deferred to existing LS-017–023 pipeline");

        return Task.FromResult(new GovernanceExecutionResult
        {
            DecisionType       = GovernanceDecisionTypes.Allow,
            ReasonCode         = GovernanceReasonCodes.SmsEnforced,
            ChannelType        = ChannelType,
            TenantId           = context.TenantId,
            MatchedRuleIds     = Array.Empty<Guid>(),
            MatchedRulePackIds = Array.Empty<Guid>(),
            AppliedOverlayIds  = Array.Empty<Guid>(),
            ShouldProceed      = true,
            ShouldWarn         = false,
            ShouldBlock        = false,
            RequiresReview     = false,
            EngineStatus       = "compatibility_passthrough",
            SafeMetadata       = new Dictionary<string, string>
            {
                ["note"] = "SMS enforcement active via LS-017 through LS-023 pipeline."
            }
        });
    }

    /// <summary>
    /// Simulation uses GovernanceRuleEvaluationHelper for SMS rules — no live SMS persistence.
    /// </summary>
    public async Task<GovernanceSimulationResult> SimulateAsync(
        GovernanceSimulationRequest request,
        GovernanceTopologyGraph topology,
        CancellationToken ct = default)
    {
        var start   = DateTime.UtcNow;

        GovernanceExecutionResult result;
        if (string.IsNullOrWhiteSpace(request.SimulationPayloadText))
        {
            result = new GovernanceExecutionResult
            {
                DecisionType   = GovernanceDecisionTypes.Allow,
                ReasonCode     = GovernanceReasonCodes.InsufficientContext,
                ChannelType    = ChannelType,
                TenantId       = request.TenantId,
                ShouldProceed  = true,
                EngineStatus   = "fail_open"
            };
        }
        else
        {
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

                var shouldBlock  = evalResult.DecisionType == GovernanceDecisionTypes.Block;
                var shouldReview = evalResult.DecisionType == GovernanceDecisionTypes.ReviewRequired;
                var shouldWarn   = evalResult.DecisionType == GovernanceDecisionTypes.Warn;

                result = new GovernanceExecutionResult
                {
                    DecisionType          = evalResult.DecisionType,
                    ReasonCode            = evalResult.ReasonCode,
                    ChannelType           = ChannelType,
                    TenantId              = context.TenantId,
                    MatchedRuleIds        = evalResult.MatchedRuleIds,
                    MatchedRulePackIds    = evalResult.MatchedPackIds,
                    ContentClassification = evalResult.ContentClassification,
                    ShouldProceed         = !shouldBlock && !shouldReview,
                    ShouldWarn            = shouldWarn,
                    ShouldBlock           = shouldBlock,
                    RequiresReview        = shouldReview,
                    EngineStatus          = "ok"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SmsGovernanceCompatibilityEngine: simulation error — fail open");
                result = new GovernanceExecutionResult
                {
                    DecisionType   = GovernanceDecisionTypes.Allow,
                    ReasonCode     = GovernanceReasonCodes.ChannelEngineFailed,
                    ChannelType    = ChannelType,
                    TenantId       = request.TenantId,
                    ShouldProceed  = true,
                    EngineStatus   = "fail_open"
                };
            }
        }

        var warnings = new List<string>(topology.Warnings)
        {
            "SMS simulation uses federation topology rules only. Live SMS enforcement runs through LS-017–023 pipeline and may differ."
        };

        return new GovernanceSimulationResult
        {
            Execution          = result,
            RulesEvaluated     = topology.FinalRuleCount,
            EvaluationDuration = DateTime.UtcNow - start,
            SimulationWarnings = warnings
        };
    }
}
