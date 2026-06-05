using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-019: Governance simulation — dry-run without sending SMS.
///
/// Combines LS-018 static classification + LS-019 dynamic rule engine.
/// MUST NOT send SMS.
/// MUST NOT persist live decisions unless PersistDecision = true.
/// </summary>
public sealed class SmsGovernanceSimulationService : ISmsGovernanceSimulationService
{
    private readonly ISmsTemplateGovernanceService        _templateGovernance;
    private readonly ISmsGovernanceRuleEngine             _ruleEngine;
    private readonly ILogger<SmsGovernanceSimulationService> _logger;

    public SmsGovernanceSimulationService(
        ISmsTemplateGovernanceService           templateGovernance,
        ISmsGovernanceRuleEngine                ruleEngine,
        ILogger<SmsGovernanceSimulationService> logger)
    {
        _templateGovernance = templateGovernance;
        _ruleEngine         = ruleEngine;
        _logger             = logger;
    }

    public async Task<SmsGovernanceSimulationResponse> SimulateAsync(
        SmsGovernanceSimulationRequest request,
        CancellationToken ct = default)
    {
        var warnings   = new List<string>();
        var trace      = new List<SmsGovernanceSimulationTrace>();
        var response   = new SmsGovernanceSimulationResponse
        {
            SimulatedAt = DateTime.UtcNow,
        };

        // ── Step 1: LS-018 static governance (dry-run) ────────────────────────
        var staticDecision = "allow";
        var staticReason   = "governance_disabled";
        var classification = request.ContentClassification;

        try
        {
            var staticReq = new SmsTemplateGovernanceRequest
            {
                TenantId       = request.TenantId ?? Guid.Empty,
                TemplateKey    = request.TemplateKey,
                RenderedBody   = request.RenderedBody,
                InlineBody     = request.RenderedBody,
                TemplateBody   = request.TemplateBody,
                VariablesUsed  = request.Variables,
                IsDryRun       = true,   // ← simulation: do not persist real decision
            };

            var staticResult = await _templateGovernance.EvaluateAsync(staticReq, ct);

            staticDecision = staticResult.DecisionType;
            staticReason   = staticResult.ReasonCode;

            if (!string.IsNullOrEmpty(staticResult.Classification))
                classification = staticResult.Classification;

            if (request.IncludeRuleTrace)
            {
                trace.Add(new SmsGovernanceSimulationTrace
                {
                    Step         = "ls018_static_governance",
                    DecisionType = staticDecision,
                    ReasonCode   = staticReason,
                    Blocked      = staticResult.ShouldBlock,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernanceSimulationService: static governance threw for tenant {TenantId} — using allow",
                request.TenantId);
            warnings.Add("Static governance evaluation failed — result may be incomplete");
            staticDecision = "allow";
            staticReason   = "static_governance_error";
        }

        response.StaticDecision    = staticDecision;
        response.StaticReasonCode  = staticReason;

        // ── Step 2: LS-019 dynamic rule engine ────────────────────────────────
        var dynamicDecision = "allow";
        var dynamicReason   = "no_matching_rule";
        var matchedRules    = new List<SmsGovernanceSimulationMatchedRule>();

        try
        {
            var dynamicReq = new SmsGovernanceRuleEvaluationRequest
            {
                TenantId             = request.TenantId,
                RenderedBody         = request.RenderedBody,
                TemplateBody         = request.TemplateBody,
                Variables            = request.Variables,
                ContentClassification = classification,
                Context              = request.Context,
                IsDryRun             = true,
                NowUtc               = DateTime.UtcNow,
            };

            var dynamicResult = await _ruleEngine.EvaluateContentAsync(dynamicReq, ct);

            dynamicDecision = dynamicResult.DecisionType;
            dynamicReason   = dynamicResult.ReasonCode;

            if (!string.IsNullOrEmpty(dynamicResult.EffectiveClassification))
                classification = dynamicResult.EffectiveClassification;

            response.EnforcementMode = dynamicResult.EnforcementMode;
            response.ProfileAssigned = false;

            matchedRules = dynamicResult.MatchedRules.Select(m => new SmsGovernanceSimulationMatchedRule
            {
                RuleId               = m.RuleId,
                RulePackId           = m.RulePackId,
                RuleName             = m.RuleName,
                RuleType             = m.RuleType,
                Severity             = m.Severity,
                MatchedPatternMasked = m.MatchedPatternMasked,
                ReasonCode           = m.ReasonCode,
            }).ToList();

            if (request.IncludeRuleTrace)
            {
                foreach (var match in dynamicResult.MatchedRules)
                {
                    trace.Add(new SmsGovernanceSimulationTrace
                    {
                        Step         = $"dynamic_rule:{match.RuleType}:{match.RuleId}",
                        DecisionType = match.Severity,
                        ReasonCode   = match.ReasonCode ?? GetDefaultReasonCode(match.RuleType),
                        Blocked      = match.Severity is "block" or "review_required",
                    });
                }

                trace.Add(new SmsGovernanceSimulationTrace
                {
                    Step         = "dynamic_rule_engine_final",
                    DecisionType = dynamicDecision,
                    ReasonCode   = dynamicReason,
                    Blocked      = dynamicResult.ShouldBlock,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernanceSimulationService: dynamic rule engine threw for tenant {TenantId} — using allow",
                request.TenantId);
            warnings.Add("Dynamic rule engine evaluation failed — result may be incomplete");
            dynamicDecision = "allow";
            dynamicReason   = "rule_engine_error";
        }

        response.DynamicDecision   = dynamicDecision;
        response.DynamicReasonCode = dynamicReason;

        // ── Step 3: Combine — stricter wins ───────────────────────────────────
        var finalDecision = StricterDecision(staticDecision, dynamicDecision);
        var finalReason   = finalDecision == staticDecision ? staticReason : dynamicReason;

        response.FinalDecision         = finalDecision;
        response.FinalReasonCode       = finalReason;
        response.ContentClassification = classification;
        response.WouldBlock            = finalDecision is "block" or "review_required";
        response.MatchedRules          = matchedRules;
        response.Warnings              = warnings;

        if (request.IncludeRuleTrace)
        {
            trace.Add(new SmsGovernanceSimulationTrace
            {
                Step         = "combined_final",
                DecisionType = finalDecision,
                ReasonCode   = finalReason,
                Blocked      = response.WouldBlock,
            });
        }

        response.RuleTrace = trace;
        return response;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, int> SeverityRank = new()
    {
        ["allow"]            = 0,
        ["warn"]             = 1,
        ["override_allowed"] = 2,
        ["review_required"]  = 3,
        ["block"]            = 4,
    };

    private static string StricterDecision(string a, string b) =>
        SeverityRank.GetValueOrDefault(a, 0) >= SeverityRank.GetValueOrDefault(b, 0) ? a : b;

    private static string GetDefaultReasonCode(string ruleType) =>
        ruleType switch
        {
            "prohibited_phrase"       => "prohibited_phrase_match",
            "restricted_pattern"      => "restricted_pattern_match",
            "classification_override" => "classification_override",
            "variable_rule"           => "variable_rule_violation",
            "link_rule"               => "restricted_domain",
            "delivery_restriction"    => "delivery_restriction_match",
            "escalation_rule"         => "escalation_rule_match",
            _                         => "rule_match",
        };
}
