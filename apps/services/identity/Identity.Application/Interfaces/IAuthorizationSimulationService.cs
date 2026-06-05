namespace Identity.Application.Interfaces;

public interface IAuthorizationSimulationService
{
    Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken ct = default);
}

public class SimulationRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public Dictionary<string, object?>? ResourceContext { get; set; }
    public Dictionary<string, string>? RequestContext { get; set; }
    public SimulationMode Mode { get; set; } = SimulationMode.Live;
    public DraftPolicyInput? DraftPolicy { get; set; }
    public List<Guid>? ExcludePolicyIds { get; set; }
}

public enum SimulationMode
{
    Live,
    Draft
}

public class DraftPolicyInput
{
    public string PolicyCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public string Effect { get; set; } = "Allow";
    public List<DraftRuleInput> Rules { get; set; } = [];
}

public class DraftRuleInput
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string LogicalGroup { get; set; } = "And";
}

public class SimulationResult
{
    public bool Allowed { get; set; }
    public bool PermissionPresent { get; set; }
    public bool RoleFallbackUsed { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public PolicyDecisionResult PolicyDecision { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public SimulationMode Mode { get; set; }
    public UserIdentitySummary User { get; set; } = new();
    public List<PermissionSourceEntry> PermissionSources { get; set; } = [];
    public long EvaluationElapsedMs { get; set; }
}

public class PolicyDecisionResult
{
    public bool Evaluated { get; set; }
    public long PolicyVersion { get; set; }
    public bool DenyOverrideApplied { get; set; }
    public string? DenyOverridePolicyCode { get; set; }
    public List<SimulatedMatchedPolicy> MatchedPolicies { get; set; } = [];
}

public class SimulatedMatchedPolicy
{
    public string PolicyCode { get; set; } = string.Empty;
    public string? PolicyName { get; set; }
    public string Effect { get; set; } = "Allow";
    public int Priority { get; set; }
    public int EvaluationOrder { get; set; }
    public string Result { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public List<SimulatedRuleResult> RuleResults { get; set; } = [];
}

public class SimulatedRuleResult
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string? Actual { get; set; }
    public bool Passed { get; set; }
}

public class UserIdentitySummary
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
}

public class PermissionSourceEntry
{
    public string PermissionCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ViaRole { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
}
