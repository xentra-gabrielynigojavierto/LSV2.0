using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Shared governance rule evaluation helper for non-SMS channels.
///
/// Loads SmsGovernanceRule records from the DB using pack IDs resolved by GovernanceTopologyGraph,
/// then applies local in-memory rule matching (prohibited phrase, restricted pattern, link rule,
/// classification override, variable rule) with ReDoS protection.
///
/// NEVER persists evaluated text, matched content, email addresses, phone numbers, or raw payloads.
/// Returns only matched rule IDs, pack IDs, severity decision, and safe classification labels.
/// </summary>
public sealed class GovernanceRuleEvaluationHelper
{
    private readonly NotificationsDbContext _db;
    private readonly IOptions<GovernanceExecutionRuntimeOptions> _options;
    private readonly ILogger<GovernanceRuleEvaluationHelper> _logger;

    // Severity ranking: allow < warn < override_allowed < review_required < block
    private static readonly Dictionary<string, int> SeverityRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["allow"]           = 0,
        ["warn"]            = 1,
        ["override_allowed"] = 2,
        ["review_required"] = 3,
        ["block"]           = 4,
    };

    private static readonly Regex CatastrophicPatternRegex = new(
        @"(\(.*\+\)|\(\?.*\+\)|\[.*\]\+|(?:\.\*){3,})",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    public GovernanceRuleEvaluationHelper(
        NotificationsDbContext db,
        IOptions<GovernanceExecutionRuntimeOptions> options,
        ILogger<GovernanceRuleEvaluationHelper> logger)
    {
        _db      = db;
        _options = options;
        _logger  = logger;
    }

    /// <summary>
    /// Evaluate text content against governance rules derived from the topology graph.
    /// subjectText and bodyText are TRANSIENT — never persisted.
    /// Returns safe result with matched IDs and decision — no raw content.
    /// </summary>
    public async Task<EvaluationHelperResult> EvaluateTextContentAsync(
        string channelType,
        Guid? tenantId,
        string? subjectText,
        string? bodyText,
        string? safeMetadataHint,
        GovernanceTopologyGraph graph,
        string? evaluationContext,
        CancellationToken ct = default)
    {
        var opts = _options.Value;

        // Collect all pack IDs from the graph
        var packIds = new HashSet<Guid>();
        foreach (var p in graph.GlobalPacks)    packIds.Add(p.RulePackId);
        foreach (var p in graph.ChannelPacks)   packIds.Add(p.RulePackId);
        foreach (var p in graph.TenantPacks)    packIds.Add(p.RulePackId);
        foreach (var p in graph.FederatedPacks) packIds.Add(p.RulePackId);

        if (packIds.Count == 0)
            return EvaluationHelperResult.Allow(GovernanceReasonCodes.NoApplicableRules);

        // Load enabled rules for these packs
        List<SmsGovernanceRule> rules;
        try
        {
            rules = await _db.Set<SmsGovernanceRule>()
                .AsNoTracking()
                .Where(r => packIds.Contains(r.RulePackId) && r.Enabled)
                .OrderBy(r => r.Priority)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GovernanceRuleEvaluationHelper: failed to load rules for {Count} packs", packIds.Count);
            return EvaluationHelperResult.FailOpen();
        }

        if (rules.Count == 0)
            return EvaluationHelperResult.Allow(GovernanceReasonCodes.NoApplicableRules);

        // Truncate evaluation text to configured max
        var maxLen = opts.MaxEvaluationTextLength;
        var subject = Truncate(subjectText, maxLen);
        var body    = Truncate(bodyText, maxLen);
        var combined = string.Concat(subject ?? string.Empty, " ", body ?? string.Empty).Trim();

        var matchedRuleIds    = new List<Guid>();
        var matchedPackIds    = new HashSet<Guid>();
        var currentSeverity   = "allow";
        var currentRank       = 0;
        var currentReason     = GovernanceReasonCodes.NoApplicableRules;
        string? contentClassification = null;

        foreach (var rule in rules)
        {
            try
            {
                var matchResult = EvaluateRule(rule, subject, body, combined, safeMetadataHint, opts, evaluationContext);
                if (matchResult == null) continue;

                var rank = SeverityRank.GetValueOrDefault(rule.Severity, 0);
                if (rank > currentRank)
                {
                    currentRank     = rank;
                    currentSeverity = rule.Severity;
                    currentReason   = MapSeverityToReason(rule.RuleType, rule.Severity);
                }

                matchedRuleIds.Add(rule.Id);
                matchedPackIds.Add(rule.RulePackId);

                if (rule.RuleType == "classification_override" && matchResult.Classification != null)
                    contentClassification = matchResult.Classification;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GovernanceRuleEvaluationHelper: rule {RuleId} evaluation failed — skipping", rule.Id);
            }
        }

        var decisionType = MapSeverityToDecision(currentSeverity);

        return new EvaluationHelperResult(
            DecisionType:         decisionType,
            ReasonCode:           matchedRuleIds.Count == 0 ? GovernanceReasonCodes.NoApplicableRules : currentReason,
            MatchedRuleIds:       matchedRuleIds,
            MatchedPackIds:       matchedPackIds.ToList(),
            ContentClassification: contentClassification,
            TotalRulesEvaluated:  rules.Count);
    }

    // ─── Rule dispatching ────────────────────────────────────────────────────

    private RuleMatchResult? EvaluateRule(
        SmsGovernanceRule rule,
        string? subject,
        string? body,
        string combined,
        string? safeMetadata,
        GovernanceExecutionRuntimeOptions opts,
        string? evaluationContext)
    {
        return rule.RuleType switch
        {
            "prohibited_phrase"      => EvalProhibitedPhrase(rule, combined, opts),
            "restricted_pattern"     => EvalRestrictedPattern(rule, combined, opts),
            "link_rule"              => EvalLinkRule(rule, combined, opts),
            "classification_override" => EvalClassificationOverride(rule, combined, safeMetadata),
            "variable_rule"          => EvalVariableRule(rule, combined),
            "delivery_restriction"   => EvalDeliveryRestriction(rule, evaluationContext),
            _ => null  // escalation_rule and unknown types: not applicable to content evaluation
        };
    }

    private static RuleMatchResult? EvalProhibitedPhrase(
        SmsGovernanceRule rule, string text, GovernanceExecutionRuntimeOptions opts)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        if (rule.Pattern.Length > 500) return null;

        bool wholeWord = false;
        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                var meta = JsonDocument.Parse(rule.MetadataJson);
                if (meta.RootElement.TryGetProperty("wholeWord", out var ww))
                    wholeWord = ww.GetBoolean();
            }
            catch { /* ignore bad metadata */ }
        }

        if (wholeWord)
        {
            if (!ContainsWholeWord(text, rule.Pattern)) return null;
        }
        else
        {
            if (!text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) return null;
        }

        return new RuleMatchResult(null);
    }

    private RuleMatchResult? EvalRestrictedPattern(
        SmsGovernanceRule rule, string text, GovernanceExecutionRuntimeOptions opts)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        if (rule.Pattern.Length > 500) return null;

        // Reject catastrophic patterns
        try
        {
            if (CatastrophicPatternRegex.IsMatch(rule.Pattern))
            {
                _logger.LogWarning("GovernanceRuleEvaluationHelper: skipping potentially catastrophic regex pattern for rule {Id}", rule.Id);
                return null;
            }
        }
        catch { return null; }

        try
        {
            var timeout = TimeSpan.FromMilliseconds(opts.RegexTimeoutMs);
            var match = Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase, timeout);
            return match ? new RuleMatchResult(null) : null;
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning("GovernanceRuleEvaluationHelper: regex timeout for rule {Id} — skipping", rule.Id);
            return null;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "GovernanceRuleEvaluationHelper: invalid regex pattern for rule {Id} — skipping", rule.Id);
            return null;
        }
    }

    private static RuleMatchResult? EvalLinkRule(
        SmsGovernanceRule rule, string text, GovernanceExecutionRuntimeOptions opts)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        // Link rule: pattern is a domain or URL fragment
        if (!text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) return null;
        return new RuleMatchResult(null);
    }

    private static RuleMatchResult? EvalClassificationOverride(
        SmsGovernanceRule rule, string text, string? safeMetadata)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        // Classification: match against combined text or metadata hint
        var target = string.Concat(text, " ", safeMetadata ?? string.Empty);
        if (!target.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) return null;

        string? label = null;
        if (!string.IsNullOrEmpty(rule.MetadataJson))
        {
            try
            {
                var meta = JsonDocument.Parse(rule.MetadataJson);
                if (meta.RootElement.TryGetProperty("classification", out var cls))
                    label = cls.GetString();
            }
            catch { /* ignore */ }
        }

        return new RuleMatchResult(label);
    }

    private static RuleMatchResult? EvalVariableRule(SmsGovernanceRule rule, string text)
    {
        if (string.IsNullOrEmpty(rule.Pattern)) return null;
        // Variable rule: detect unresolved template tokens like {{variableName}}
        var varPattern = $"{{{{{rule.Pattern}}}}}";
        if (!text.Contains(varPattern, StringComparison.OrdinalIgnoreCase)) return null;
        return new RuleMatchResult(null);
    }

    private static RuleMatchResult? EvalDeliveryRestriction(SmsGovernanceRule rule, string? evaluationContext)
    {
        if (string.IsNullOrEmpty(rule.Pattern) || string.IsNullOrEmpty(evaluationContext)) return null;
        if (!evaluationContext.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) return null;
        return new RuleMatchResult(null);
    }

    // ─── Severity / decision mapping ─────────────────────────────────────────

    private static string MapSeverityToDecision(string severity) => severity switch
    {
        "block"            => GovernanceDecisionTypes.Block,
        "review_required"  => GovernanceDecisionTypes.ReviewRequired,
        "warn"             => GovernanceDecisionTypes.Warn,
        "override_allowed" => GovernanceDecisionTypes.Allow,
        _                  => GovernanceDecisionTypes.Allow,
    };

    private static string MapSeverityToReason(string ruleType, string severity) => ruleType switch
    {
        "prohibited_phrase" => severity == "block"
            ? GovernanceReasonCodes.ProhibitedContent
            : GovernanceReasonCodes.RestrictedContent,
        "restricted_pattern" => GovernanceReasonCodes.RestrictedContent,
        "link_rule"          => GovernanceReasonCodes.RestrictedContent,
        "classification_override" => GovernanceReasonCodes.RestrictedContent,
        "variable_rule"      => GovernanceReasonCodes.UnsafePayload,
        "delivery_restriction" => GovernanceReasonCodes.RestrictedContent,
        _                    => GovernanceReasonCodes.RuleMatch,
    };

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static bool ContainsWholeWord(string text, string phrase)
    {
        var idx = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            var before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var after  = idx + phrase.Length >= text.Length || !char.IsLetterOrDigit(text[idx + phrase.Length]);
            if (before && after) return true;
            idx = text.IndexOf(phrase, idx + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static string? Truncate(string? text, int maxLen)
    {
        if (text == null || text.Length <= maxLen) return text;
        return text[..maxLen];
    }

    private sealed record RuleMatchResult(string? Classification);
}

// ---------------------------------------------------------------------------
// Result record
// ---------------------------------------------------------------------------

public sealed record EvaluationHelperResult(
    string              DecisionType,
    string              ReasonCode,
    IReadOnlyList<Guid> MatchedRuleIds,
    IReadOnlyList<Guid> MatchedPackIds,
    string?             ContentClassification,
    int                 TotalRulesEvaluated)
{
    public static EvaluationHelperResult Allow(string reason = GovernanceReasonCodes.NoApplicableRules) =>
        new(GovernanceDecisionTypes.Allow, reason, Array.Empty<Guid>(), Array.Empty<Guid>(), null, 0);

    public static EvaluationHelperResult FailOpen() =>
        new(GovernanceDecisionTypes.Allow, GovernanceReasonCodes.FailOpen, Array.Empty<Guid>(), Array.Empty<Guid>(), null, 0);
}
