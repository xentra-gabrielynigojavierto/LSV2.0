namespace BuildingBlocks.Authorization;

public class PolicyEvaluationResult
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<MatchedPolicy> MatchedPolicies { get; set; } = [];
    public bool DenyOverrideApplied { get; set; }
    public string? DenyOverridePolicyCode { get; set; }
    public long EvaluationElapsedMs { get; set; }
    public bool ResourceContextPresent { get; set; }
    public long PolicyVersion { get; set; }
    public bool CacheHit { get; set; }

    public static PolicyEvaluationResult Allow(string reason = "No policies attached") =>
        new() { Allowed = true, Reason = reason };

    public static PolicyEvaluationResult Deny(string reason, List<MatchedPolicy>? matchedPolicies = null) =>
        new() { Allowed = false, Reason = reason, MatchedPolicies = matchedPolicies ?? [] };

    public static PolicyEvaluationResult AllowWithPolicies(string reason, List<MatchedPolicy> matchedPolicies) =>
        new() { Allowed = true, Reason = reason, MatchedPolicies = matchedPolicies };

    public static PolicyEvaluationResult DenyWithOverride(string reason, string denyPolicyCode, List<MatchedPolicy> matchedPolicies) =>
        new()
        {
            Allowed = false,
            Reason = reason,
            MatchedPolicies = matchedPolicies,
            DenyOverrideApplied = true,
            DenyOverridePolicyCode = denyPolicyCode,
        };
}

public class MatchedPolicy
{
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string Effect { get; set; } = "Allow";
    public int Priority { get; set; }
    public int EvaluationOrder { get; set; }
    public bool Passed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<RuleResult> RuleResults { get; set; } = [];
}

public class RuleResult
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string? ActualValue { get; set; }
    public bool Passed { get; set; }
}
