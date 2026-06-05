using System.Diagnostics;
using BuildingBlocks.Authorization;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class AuthorizationSimulationService : IAuthorizationSimulationService
{
    private readonly IdentityDbContext _db;
    private readonly IEffectiveAccessService _effectiveAccessService;
    private readonly IPolicyVersionProvider _versionProvider;
    private readonly ILogger<AuthorizationSimulationService> _logger;

    public AuthorizationSimulationService(
        IdentityDbContext db,
        IEffectiveAccessService effectiveAccessService,
        IPolicyVersionProvider versionProvider,
        ILogger<AuthorizationSimulationService> logger)
    {
        _db = db;
        _effectiveAccessService = effectiveAccessService;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public async Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var user = await _db.Users
            .Where(u => u.Id == request.UserId && u.TenantId == request.TenantId)
            .Select(u => new { u.Id, u.TenantId, u.Email, u.FirstName, u.LastName })
            .FirstOrDefaultAsync(ct);

        if (user == null)
        {
            sw.Stop();
            return new SimulationResult
            {
                Allowed = false,
                PermissionCode = request.PermissionCode,
                Reason = "Target user not found or does not belong to the specified tenant.",
                Mode = request.Mode,
                EvaluationElapsedMs = sw.ElapsedMilliseconds,
            };
        }

        var effectiveAccess = await _effectiveAccessService.GetEffectiveAccessAsync(request.TenantId, request.UserId, ct);

        var userSummary = new UserIdentitySummary
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            Roles = effectiveAccess.ProductRolesFlat.Concat(effectiveAccess.TenantRoles).ToList(),
            Permissions = effectiveAccess.Permissions,
        };

        var permissionPresent = effectiveAccess.Permissions
            .Any(p => string.Equals(p, request.PermissionCode, StringComparison.OrdinalIgnoreCase));

        var permissionSources = effectiveAccess.PermissionSources
            .Where(ps => string.Equals(ps.PermissionCode, request.PermissionCode, StringComparison.OrdinalIgnoreCase))
            .Select(ps => new PermissionSourceEntry
            {
                PermissionCode = ps.PermissionCode,
                Source = ps.Source,
                ViaRole = ps.ViaRoleCode,
                GroupId = ps.GroupId,
                GroupName = ps.GroupName,
            })
            .ToList();

        var roleFallbackUsed = !permissionPresent && effectiveAccess.ProductRolesFlat.Count > 0;

        var isAdmin = effectiveAccess.TenantRoles.Any(r =>
            string.Equals(r, "TenantAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "PlatformAdmin", StringComparison.OrdinalIgnoreCase));

        var policyVersion = _versionProvider.CurrentVersion;

        var policyDecision = await EvaluatePoliciesAsync(request, policyVersion, ct);

        bool allowed;
        string reason;

        if (isAdmin)
        {
            allowed = true;
            reason = "Admin bypass — user has admin role, access would be granted automatically.";
        }
        else if (!permissionPresent && !roleFallbackUsed)
        {
            allowed = false;
            reason = $"Permission '{request.PermissionCode}' is not present in the user's effective permissions.";
        }
        else if (!policyDecision.Evaluated)
        {
            allowed = permissionPresent;
            reason = permissionPresent
                ? "Permission present and no policies attached — access granted."
                : "Permission not directly present; role fallback would apply but no policies found.";
        }
        else if (policyDecision.DenyOverrideApplied)
        {
            allowed = false;
            reason = $"Deny override applied by policy '{policyDecision.DenyOverridePolicyCode}' — access denied.";
        }
        else
        {
            var failedPolicies = policyDecision.MatchedPolicies
                .Where(mp => mp.Effect == "Allow" && mp.Result == "DENY")
                .ToList();

            if (failedPolicies.Count > 0)
            {
                allowed = false;
                reason = $"Failed {failedPolicies.Count} allow polic{(failedPolicies.Count != 1 ? "ies" : "y")}: {string.Join(", ", failedPolicies.Select(p => p.PolicyCode))}";
            }
            else
            {
                allowed = permissionPresent || roleFallbackUsed;
                reason = "Permission present and all active policies passed.";
            }
        }

        sw.Stop();

        return new SimulationResult
        {
            Allowed = allowed,
            PermissionPresent = permissionPresent,
            RoleFallbackUsed = roleFallbackUsed,
            PermissionCode = request.PermissionCode,
            PolicyDecision = policyDecision,
            Reason = reason,
            Mode = request.Mode,
            User = userSummary,
            PermissionSources = permissionSources,
            EvaluationElapsedMs = sw.ElapsedMilliseconds,
        };
    }

    private async Task<PolicyDecisionResult> EvaluatePoliciesAsync(
        SimulationRequest request, long policyVersion, CancellationToken ct)
    {
        var permissionPolicies = await _db.PermissionPolicies
            .Where(pp => pp.PermissionCode == request.PermissionCode && pp.IsActive)
            .Select(pp => pp.PolicyId)
            .ToListAsync(ct);

        if (request.ExcludePolicyIds?.Count > 0)
            permissionPolicies = permissionPolicies.Where(id => !request.ExcludePolicyIds.Contains(id)).ToList();

        var policies = permissionPolicies.Count > 0
            ? await _db.Policies
                .Where(p => permissionPolicies.Contains(p.Id) && p.IsActive)
                .Include(p => p.Rules)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.PolicyCode)
                .ThenBy(p => p.Id)
                .ToListAsync(ct)
            : new List<Policy>();

        var hasDraftPolicy = request.Mode == SimulationMode.Draft && request.DraftPolicy != null;

        if (policies.Count == 0 && !hasDraftPolicy)
        {
            return new PolicyDecisionResult
            {
                Evaluated = false,
                PolicyVersion = policyVersion,
            };
        }

        var resourceAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (request.ResourceContext != null)
        {
            foreach (var kvp in request.ResourceContext)
                resourceAttrs[kvp.Key] = kvp.Value;
        }

        var requestAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (request.RequestContext != null)
        {
            foreach (var kvp in request.RequestContext)
                requestAttrs[kvp.Key] = kvp.Value;
        }

        var allAttributes = PolicyEvaluationService.MergeAttributes(resourceAttrs, requestAttrs);

        var matchedPolicies = new List<SimulatedMatchedPolicy>();
        SimulatedMatchedPolicy? firstDenyOverride = null;
        var evalOrder = 0;

        foreach (var policy in policies)
        {
            var mp = PolicyEvaluationService.EvaluatePolicy(policy, allAttributes, evalOrder);
            var simulated = ToSimulatedMatchedPolicy(mp, policy.Effect == PolicyEffect.Deny && mp.Passed, false);

            matchedPolicies.Add(simulated);

            if (policy.Effect == PolicyEffect.Deny && mp.Passed && firstDenyOverride == null)
                firstDenyOverride = simulated;

            evalOrder++;
        }

        if (hasDraftPolicy)
        {
            var draftResult = EvaluateDraftPolicy(request.DraftPolicy!, allAttributes, evalOrder);
            matchedPolicies.Add(draftResult);

            if (draftResult.Effect == "Deny" && draftResult.Result == "ALLOW" && firstDenyOverride == null)
                firstDenyOverride = draftResult;
        }

        return new PolicyDecisionResult
        {
            Evaluated = true,
            PolicyVersion = policyVersion,
            DenyOverrideApplied = firstDenyOverride != null,
            DenyOverridePolicyCode = firstDenyOverride?.PolicyCode,
            MatchedPolicies = matchedPolicies,
        };
    }

    private static SimulatedMatchedPolicy EvaluateDraftPolicy(
        DraftPolicyInput draft, Dictionary<string, object?> attributes, int evaluationOrder)
    {
        var ruleResults = new List<SimulatedRuleResult>();
        var andResults = new List<bool>();
        var orResults = new List<bool>();

        foreach (var rule in draft.Rules)
        {
            if (!Enum.TryParse<RuleOperator>(rule.Operator, true, out var op))
                op = RuleOperator.Equals;

            attributes.TryGetValue(rule.Field, out var rawValue);
            var actualValue = rawValue?.ToString();
            var passed = PolicyEvaluationService.EvaluateOperator(op, actualValue, rule.Value, rawValue);

            ruleResults.Add(new SimulatedRuleResult
            {
                Field = rule.Field,
                Operator = rule.Operator,
                Expected = rule.Value,
                Actual = actualValue,
                Passed = passed,
            });

            var isOr = string.Equals(rule.LogicalGroup, "Or", StringComparison.OrdinalIgnoreCase);
            if (isOr) orResults.Add(passed);
            else andResults.Add(passed);
        }

        bool allPassed;
        if (orResults.Count > 0 && andResults.Count > 0)
            allPassed = andResults.All(r => r) && orResults.Any(r => r);
        else if (orResults.Count > 0)
            allPassed = orResults.Any(r => r);
        else if (andResults.Count > 0)
            allPassed = andResults.All(r => r);
        else
            allPassed = true;

        var isDeny = string.Equals(draft.Effect, "Deny", StringComparison.OrdinalIgnoreCase);
        var resultStr = allPassed
            ? (isDeny ? "DENY" : "ALLOW")
            : (isDeny ? "ALLOW" : "DENY");

        return new SimulatedMatchedPolicy
        {
            PolicyCode = draft.PolicyCode,
            PolicyName = draft.Name,
            Effect = draft.Effect,
            Priority = draft.Priority,
            EvaluationOrder = evaluationOrder,
            Result = resultStr,
            IsDraft = true,
            RuleResults = ruleResults,
        };
    }

    private static SimulatedMatchedPolicy ToSimulatedMatchedPolicy(MatchedPolicy mp, bool isDenyMatch, bool isDraft)
    {
        var resultStr = mp.Passed
            ? (mp.Effect == "Deny" ? "DENY" : "ALLOW")
            : (mp.Effect == "Deny" ? "ALLOW" : "DENY");

        return new SimulatedMatchedPolicy
        {
            PolicyCode = mp.PolicyCode,
            PolicyName = mp.PolicyName,
            Effect = mp.Effect,
            Priority = mp.Priority,
            EvaluationOrder = mp.EvaluationOrder,
            Result = resultStr,
            IsDraft = isDraft,
            RuleResults = mp.RuleResults.Select(rr => new SimulatedRuleResult
            {
                Field = rr.Field,
                Operator = rr.Operator,
                Expected = rr.ExpectedValue,
                Actual = rr.ActualValue,
                Passed = rr.Passed,
            }).ToList(),
        };
    }
}
