using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-019: Dynamic governance rule engine.
///
/// Evaluates all resolved rules in priority order against SMS content.
/// No AI/ML, no external APIs, no raw phone persistence.
/// Evaluation failures fail-open by default.
///
/// Decision severity ordering (ascending strictness):
///   allow < warn < override_allowed < review_required < block
///
/// EnforcementMode adjustments (applied after rule evaluation):
///   permissive — block → review_required
///   strict     — review_required → block
///   standard   — decisions respected as-is
/// </summary>
public sealed partial class SmsGovernanceRuleEngine : ISmsGovernanceRuleEngine
{
    private static readonly string[] RuleTypes =
    [
        "prohibited_phrase", "restricted_pattern", "classification_override",
        "variable_rule", "link_rule", "delivery_restriction", "escalation_rule",
    ];

    private static readonly string[] Severities =
        ["allow", "warn", "override_allowed", "review_required", "block"];

    private static readonly Dictionary<string, int> SeverityRank = new()
    {
        ["allow"]           = 0,
        ["warn"]            = 1,
        ["override_allowed"] = 2,
        ["review_required"] = 3,
        ["block"]           = 4,
    };

    // Catastrophic backtracking patterns: nested quantifiers, (a+)+, etc.
    [GeneratedRegex(@"(\(.*\+.*\)\+)|(\(.*\*.*\)\+)|(\(.*\+.*\)\*)|(\(.*\{.*\}.*\)\+)")]
    private static partial Regex CatastrophicPatternRegex();

    // URL extraction for link rules
    [GeneratedRegex(@"https?://([a-zA-Z0-9\-\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DomainExtractRegex();

    private readonly ISmsGovernanceRuleResolver           _resolver;
    private readonly SmsGovernanceDynamicOptions          _options;
    private readonly ILogger<SmsGovernanceRuleEngine>     _logger;

    // LS-NOTIF-SMS-020: nullable match recorder — fire-and-forget, never blocks delivery
    private readonly ISmsGovernanceMatchRecorder?         _recorder;

    public SmsGovernanceRuleEngine(
        ISmsGovernanceRuleResolver           resolver,
        IOptions<SmsGovernanceDynamicOptions> options,
        ILogger<SmsGovernanceRuleEngine>      logger,
        ISmsGovernanceMatchRecorder?          recorder = null)
    {
        _resolver = resolver;
        _options  = options.Value;
        _logger   = logger;
        _recorder = recorder;
    }

    public Task<SmsGovernanceRuleEvaluationResult> EvaluateContentAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default) =>
        EvaluateAsync(request, context: "content", ct);

    public Task<SmsGovernanceRuleEvaluationResult> EvaluateTemplateAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default) =>
        EvaluateAsync(request, context: "template", ct);

    public Task<SmsGovernanceRuleEvaluationResult> EvaluateEscalationPayloadAsync(
        SmsGovernanceRuleEvaluationRequest request,
        CancellationToken ct = default) =>
        EvaluateAsync(request, context: "escalation", ct);

    // ─── Core evaluation loop ─────────────────────────────────────────────────

    private async Task<SmsGovernanceRuleEvaluationResult> EvaluateAsync(
        SmsGovernanceRuleEvaluationRequest request,
        string context,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return Allow("governance_disabled");

        try
        {
            var resolution = await _resolver.ResolveRulesAsync(request.TenantId, context, ct);

            if (resolution.Rules.Count == 0)
                return AllowWithEnforcement("no_matching_rule", resolution.EnforcementMode, null);

            var body           = request.RenderedBody ?? request.TemplateBody ?? string.Empty;
            var matchedRules   = new List<SmsGovernanceRuleMatch>();
            var classification = request.ContentClassification;
            var bestDecision   = "allow";
            var bestReasonCode = "no_matching_rule";

            foreach (var rule in resolution.Rules)
            {
                if (!IsApplicableToContext(rule, context)) continue;

                var match = EvaluateRule(rule, body, request.Variables, classification, request.NowUtc);
                if (match == null) continue;

                matchedRules.Add(match);

                // Track highest severity
                if (SeverityRank.TryGetValue(match.Severity, out var rank) &&
                    rank > SeverityRank.GetValueOrDefault(bestDecision, 0))
                {
                    bestDecision   = match.Severity;
                    bestReasonCode = match.ReasonCode ?? GetDefaultReasonCode(rule.RuleType);
                }

                // Apply classification_override side-effect
                if (rule.RuleType == "classification_override" && match.Metadata.TryGetValue("targetClassification", out var tc))
                    classification = tc?.ToString();
            }

            // Apply enforcement mode
            var finalDecision = ApplyEnforcementMode(bestDecision, resolution.EnforcementMode);

            var evalResult = new SmsGovernanceRuleEvaluationResult
            {
                DecisionType             = finalDecision,
                ReasonCode               = bestReasonCode,
                MatchedRules             = matchedRules,
                EffectiveClassification  = classification,
                EnforcementMode          = resolution.EnforcementMode,
                Metadata                 = new Dictionary<string, object>
                {
                    ["packsResolved"]  = resolution.Packs.Count,
                    ["rulesEvaluated"] = resolution.Rules.Count,
                    ["rulesMatched"]   = matchedRules.Count,
                },
            };

            // LS-NOTIF-SMS-020: fire-and-forget match recording for analytics
            if (matchedRules.Count > 0)
                _recorder?.RecordMatches(evalResult, request.TenantId, request.IsDryRun);

            return evalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsGovernanceRuleEngine: evaluation failed for tenant {TenantId} context={Context} — {Behavior}",
                request.TenantId, context,
                _options.FailOpenOnEvaluationError ? "failing open" : "failing closed");

            return _options.FailOpenOnEvaluationError
                ? Allow("rule_engine_error")
                : new SmsGovernanceRuleEvaluationResult
                    { DecisionType = "block", ReasonCode = "rule_engine_error" };
        }
    }

    // ─── Per-rule evaluation ─────────────────────────────────────────────────

    private SmsGovernanceRuleMatch? EvaluateRule(
        SmsGovernanceRule rule,
        string body,
        Dictionary<string, string>? variables,
        string? classification,
        DateTime nowUtc)
    {
        try
        {
            return rule.RuleType switch
            {
                "prohibited_phrase"      => EvalProhibitedPhrase(rule, body),
                "restricted_pattern"     => EvalRestrictedPattern(rule, body),
                "classification_override" => EvalClassificationOverride(rule, classification),
                "variable_rule"          => EvalVariableRule(rule, body, variables),
                "link_rule"              => EvalLinkRule(rule, body),
                "delivery_restriction"   => EvalDeliveryRestriction(rule, body, classification, nowUtc),
                "escalation_rule"        => EvalEscalationRule(rule, body, classification),
                _                        => null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernanceRuleEngine: rule {RuleId} ({RuleType}) evaluation threw — skipping",
                rule.Id, rule.RuleType);
            return null;
        }
    }

    // ─── prohibited_phrase ───────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalProhibitedPhrase(
        SmsGovernanceRule rule, string body)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        var phrase     = rule.Pattern;
        var wholeWord  = false;

        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.MetadataJson);
                if (doc.RootElement.TryGetProperty("wholeWord", out var ww) && ww.ValueKind == JsonValueKind.True)
                    wholeWord = true;
            }
            catch { }
        }

        bool found;
        if (wholeWord)
        {
            // Word boundary matching without Regex to avoid ReDoS — simple char scan
            found = ContainsWholeWord(body, phrase);
        }
        else
        {
            found = body.Contains(phrase, StringComparison.OrdinalIgnoreCase);
        }

        if (!found) return null;

        return MakeMatch(rule, "prohibited_phrase_match",
            MaskPattern(phrase));
    }

    private static bool ContainsWholeWord(string body, string phrase)
    {
        var idx = 0;
        var lower  = body.ToLowerInvariant();
        var lowerP = phrase.ToLowerInvariant();

        while (true)
        {
            idx = lower.IndexOf(lowerP, idx, StringComparison.Ordinal);
            if (idx < 0) return false;

            bool leftBound  = idx == 0                      || !char.IsLetterOrDigit(body[idx - 1]);
            bool rightBound = idx + phrase.Length >= body.Length || !char.IsLetterOrDigit(body[idx + phrase.Length]);

            if (leftBound && rightBound) return true;
            idx++;
        }
    }

    // ─── restricted_pattern ──────────────────────────────────────────────────

    private SmsGovernanceRuleMatch? EvalRestrictedPattern(
        SmsGovernanceRule rule, string body)
    {
        if (!_options.AllowRegexRules || string.IsNullOrEmpty(rule.Pattern)) return null;

        // Safety: max length already enforced at creation; double-check here
        if (rule.Pattern.Length > _options.MaxPatternLength)
        {
            _logger.LogWarning(
                "SmsGovernanceRuleEngine: rule {RuleId} pattern exceeds MaxPatternLength — skipping",
                rule.Id);
            return null;
        }

        try
        {
            var timeout = TimeSpan.FromMilliseconds(_options.RegexTimeoutMs);
            var rx      = new Regex(rule.Pattern, RegexOptions.IgnoreCase, timeout);
            if (!rx.IsMatch(body)) return null;

            return MakeMatch(rule, "restricted_pattern_match", MaskPattern(rule.Pattern));
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning(
                "SmsGovernanceRuleEngine: rule {RuleId} regex timed out — skipping",
                rule.Id);
            return null;
        }
    }

    // ─── classification_override ─────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalClassificationOverride(
        SmsGovernanceRule rule, string? classification)
    {
        if (string.IsNullOrEmpty(classification) || string.IsNullOrEmpty(rule.Pattern))
            return null;

        if (!classification.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            return null;

        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.MetadataJson);
                if (doc.RootElement.TryGetProperty("targetClassification", out var tc))
                    metadata["targetClassification"] = tc.GetString() ?? string.Empty;
            }
            catch { }
        }

        return MakeMatch(rule, "classification_override", rule.Pattern, metadata);
    }

    // ─── variable_rule ───────────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalVariableRule(
        SmsGovernanceRule rule,
        string body,
        Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;

        var varName = rule.Pattern;
        var mode    = "disallowed"; // default
        var valuePattern = string.Empty;

        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.MetadataJson);
                if (doc.RootElement.TryGetProperty("mode", out var m))
                    mode = m.GetString() ?? "disallowed";
                if (doc.RootElement.TryGetProperty("valuePattern", out var vp))
                    valuePattern = vp.GetString() ?? string.Empty;
            }
            catch { }
        }

        return mode switch
        {
            "required"  => EvalVariableRequired(rule, varName, variables),
            "disallowed" => EvalVariableDisallowed(rule, varName, body, variables),
            "value_pattern" => EvalVariableValuePattern(rule, varName, variables, valuePattern),
            _ => EvalVariableDisallowed(rule, varName, body, variables),
        };
    }

    private static SmsGovernanceRuleMatch? EvalVariableRequired(
        SmsGovernanceRule rule, string varName, Dictionary<string, string>? variables)
    {
        if (variables != null && variables.ContainsKey(varName)) return null;
        return MakeMatch(rule, "variable_rule_violation",
            MaskPattern(varName),
            new Dictionary<string, object> { ["mode"] = "required", ["variable"] = varName });
    }

    private static SmsGovernanceRuleMatch? EvalVariableDisallowed(
        SmsGovernanceRule rule, string varName,
        string body, Dictionary<string, string>? variables)
    {
        bool present = (variables != null && variables.ContainsKey(varName)) ||
                       body.Contains($"{{{{{varName}}}}}", StringComparison.OrdinalIgnoreCase);
        if (!present) return null;
        return MakeMatch(rule, "variable_rule_violation",
            MaskPattern(varName),
            new Dictionary<string, object> { ["mode"] = "disallowed", ["variable"] = varName });
    }

    private static SmsGovernanceRuleMatch? EvalVariableValuePattern(
        SmsGovernanceRule rule, string varName,
        Dictionary<string, string>? variables, string valuePattern)
    {
        if (variables == null || !variables.TryGetValue(varName, out var value)) return null;
        if (string.IsNullOrEmpty(valuePattern)) return null;

        try
        {
            if (!Regex.IsMatch(value, valuePattern, RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(100)))
                return null;
            return MakeMatch(rule, "variable_rule_violation",
                MaskPattern(varName),
                new Dictionary<string, object> { ["mode"] = "value_pattern", ["variable"] = varName });
        }
        catch { return null; }
    }

    // ─── link_rule ───────────────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalLinkRule(
        SmsGovernanceRule rule, string body)
    {
        var domains = DomainExtractRegex().Matches(body)
                          .Select(m => m.Groups[1].Value.ToLowerInvariant())
                          .Distinct()
                          .ToList();

        if (domains.Count == 0) return null;

        var mode            = "block_listed"; // default: block specific domains
        List<string>? blocked = null;
        List<string>? allowed = null;

        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.MetadataJson);
                if (doc.RootElement.TryGetProperty("mode", out var m))
                    mode = m.GetString() ?? "block_listed";
                if (doc.RootElement.TryGetProperty("blockedDomains", out var bd))
                    blocked = [.. bd.EnumerateArray().Select(e => e.GetString()!.ToLowerInvariant())];
                if (doc.RootElement.TryGetProperty("allowedDomains", out var ad))
                    allowed = [.. ad.EnumerateArray().Select(e => e.GetString()!.ToLowerInvariant())];
            }
            catch { }
        }

        // Also check the Pattern as a single blocked domain
        if (!string.IsNullOrEmpty(rule.Pattern))
        {
            blocked ??= [];
            blocked.Add(rule.Pattern.ToLowerInvariant());
        }

        bool violated = mode switch
        {
            "allow_only" => allowed != null && domains.Any(d => !allowed.Any(a => DomainMatches(d, a))),
            _            => blocked != null && domains.Any(d => blocked.Any(b => DomainMatches(d, b))),
        };

        if (!violated) return null;

        return MakeMatch(rule, mode == "allow_only" ? "disallowed_link" : "restricted_domain",
            MaskPattern(rule.Pattern ?? string.Join(",", domains.Take(2))));
    }

    private static bool DomainMatches(string domain, string pattern) =>
        domain == pattern || domain.EndsWith($".{pattern}", StringComparison.Ordinal);

    // ─── delivery_restriction ────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalDeliveryRestriction(
        SmsGovernanceRule rule, string body, string? classification, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(rule.MetadataJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rule.MetadataJson);

            // Classification-based restriction
            if (doc.RootElement.TryGetProperty("blockedClassifications", out var bclArr))
            {
                var blockedClassifications = bclArr.EnumerateArray()
                    .Select(e => e.GetString())
                    .ToHashSet();
                if (!string.IsNullOrEmpty(classification) &&
                    blockedClassifications.Contains(classification))
                {
                    return MakeMatch(rule, "delivery_restriction_match",
                        MaskPattern(classification),
                        new Dictionary<string, object> { ["reason"] = "blocked_classification" });
                }
            }

            // Keyword-based restriction
            if (!string.IsNullOrEmpty(rule.Pattern) &&
                body.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return MakeMatch(rule, "delivery_restriction_match",
                    MaskPattern(rule.Pattern),
                    new Dictionary<string, object> { ["reason"] = "keyword_match" });
            }

            // Time-of-day restriction (UTC hours)
            if (doc.RootElement.TryGetProperty("blockedUtcHoursStart", out var start) &&
                doc.RootElement.TryGetProperty("blockedUtcHoursEnd", out var end))
            {
                int s = start.GetInt32();
                int e2 = end.GetInt32();
                int h = nowUtc.Hour;
                bool inRange = s <= e2 ? h >= s && h < e2 : h >= s || h < e2;
                if (inRange)
                {
                    return MakeMatch(rule, "delivery_restriction_match", null,
                        new Dictionary<string, object> { ["reason"] = "blocked_hours" });
                }
            }
        }
        catch { }

        return null;
    }

    // ─── escalation_rule ─────────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch? EvalEscalationRule(
        SmsGovernanceRule rule, string body, string? classification)
    {
        // Match pattern against body (keyword check) or classification
        if (!string.IsNullOrEmpty(rule.Pattern))
        {
            if (classification != null &&
                classification.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return MakeMatch(rule, "escalation_rule_match",
                    MaskPattern(rule.Pattern),
                    new Dictionary<string, object> { ["matchType"] = "classification" });
            }

            if (body.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return MakeMatch(rule, "escalation_rule_match",
                    MaskPattern(rule.Pattern),
                    new Dictionary<string, object> { ["matchType"] = "keyword" });
            }
        }

        return null;
    }

    // ─── Rule context applicability ──────────────────────────────────────────

    private static bool IsApplicableToContext(SmsGovernanceRule rule, string context) =>
        (rule.RuleType, context) switch
        {
            ("escalation_rule",       "content")    => false,
            ("escalation_rule",       "template")   => false,
            ("delivery_restriction",  "template")   => false,
            _ => true,
        };

    // ─── EnforcementMode adjustment ──────────────────────────────────────────

    private static string ApplyEnforcementMode(string decision, string mode) =>
        (mode, decision) switch
        {
            ("permissive", "block")           => "review_required",
            ("strict",     "review_required") => "block",
            _                                  => decision,
        };

    // ─── Factory helpers ─────────────────────────────────────────────────────

    private static SmsGovernanceRuleMatch MakeMatch(
        SmsGovernanceRule rule,
        string reasonCode,
        string? maskedPattern = null,
        Dictionary<string, object>? extra = null)
    {
        var m = new SmsGovernanceRuleMatch
        {
            RuleId               = rule.Id,
            RulePackId           = rule.RulePackId,
            RuleName             = rule.Name,
            RuleType             = rule.RuleType,
            Severity             = rule.Severity,
            MatchedPatternMasked = maskedPattern,
            ReasonCode           = reasonCode,
            Metadata             = extra ?? [],
        };
        return m;
    }

    private static string MaskPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Empty;
        if (pattern.Length <= 4) return "****";
        return pattern[..2] + new string('*', Math.Min(pattern.Length - 2, 8));
    }

    private static string GetDefaultReasonCode(string ruleType) =>
        ruleType switch
        {
            "prohibited_phrase"      => "prohibited_phrase_match",
            "restricted_pattern"     => "restricted_pattern_match",
            "classification_override" => "classification_override",
            "variable_rule"          => "variable_rule_violation",
            "link_rule"              => "restricted_domain",
            "delivery_restriction"   => "delivery_restriction_match",
            "escalation_rule"        => "escalation_rule_match",
            _                        => "rule_match",
        };

    private static SmsGovernanceRuleEvaluationResult Allow(string reasonCode) =>
        new() { DecisionType = "allow", ReasonCode = reasonCode };

    private static SmsGovernanceRuleEvaluationResult AllowWithEnforcement(
        string reasonCode, string mode, List<SmsGovernanceRuleMatch>? matches) =>
        new()
        {
            DecisionType    = "allow",
            ReasonCode      = reasonCode,
            EnforcementMode = mode,
            MatchedRules    = matches ?? [],
        };
}
