using Flow.Application.Interfaces;
using Flow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.3 — first-phase deterministic implementation of
/// <see cref="IWorkflowTaskAssignmentResolver"/>. Pure in-code rule
/// table; no DB reads, no external calls, no rule engine.
///
/// <para>
/// <b>Lookup precedence (most-specific first):</b>
///   <list type="number">
///     <item>(<c>ProductKey</c>, <c>StepKey</c>) — product-specific
///       override.</item>
///     <item>(<c>StepKey</c>) — step-key default applied across all
///       products.</item>
///     <item>Otherwise — <see cref="WorkflowTaskAssignment.None"/>
///       (task left unassigned, the documented safe fallback).</item>
///   </list>
/// </para>
///
/// <para>
/// <b>Assignment-target precedence (User &gt; Role &gt; Org):</b>
/// enforced by the <see cref="WorkflowTaskAssignment"/> factory
/// methods which produce decisions with at most one field set. The
/// rule table therefore cannot accidentally express a multi-target
/// assignment.
/// </para>
///
/// <para>
/// <b>Adding rules:</b> code change to the static initialiser of
/// <see cref="ProductStepRules"/> / <see cref="StepRules"/> below.
/// Intentional — first-phase rules are version-controlled, reviewable,
/// and impossible to mutate at runtime. A DB-backed editor surface is
/// out of scope.
/// </para>
/// </summary>
public sealed class StaticRuleWorkflowTaskAssignmentResolver : IWorkflowTaskAssignmentResolver
{
    /// <summary>
    /// Product-scoped rules. Key is <c>(ProductKey, StepKey)</c> with
    /// case-insensitive ordinal comparison so seed data and runtime
    /// lookups remain stable across casing variations introduced by
    /// CMS / config edits.
    /// </summary>
    private static readonly IReadOnlyDictionary<(string ProductKey, string StepKey), WorkflowTaskAssignment>
        ProductStepRules =
            new Dictionary<(string, string), WorkflowTaskAssignment>(
                new ProductStepKeyComparer())
            {
                // Empty in first phase. Add product-specific overrides
                // here as workflows demand them, e.g.:
                //   { ("liens", "intake_review"), WorkflowTaskAssignment.ForRole("LiensIntakeReviewer") },
            };

    /// <summary>
    /// Step-key fallback rules, applied when no product-specific rule
    /// matches. Case-insensitive ordinal comparison.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, WorkflowTaskAssignment> StepRules =
        new Dictionary<string, WorkflowTaskAssignment>(StringComparer.OrdinalIgnoreCase)
        {
            // Empty in first phase. Add step-key defaults that are
            // safe across all products here, e.g.:
            //   { "manager_approval", WorkflowTaskAssignment.ForRole("Manager") },
        };

    private readonly ILogger<StaticRuleWorkflowTaskAssignmentResolver> _logger;

    public StaticRuleWorkflowTaskAssignmentResolver(
        ILogger<StaticRuleWorkflowTaskAssignmentResolver> logger)
    {
        _logger = logger;
    }

    public WorkflowTaskAssignment Resolve(WorkflowInstance instance, string stepKey)
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));

        // Defensive — caller (task factory) already gates on a non-empty
        // step key, but the resolver is public surface and must not
        // throw on degenerate input.
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return WorkflowTaskAssignment.None;
        }

        var productKey = instance.ProductKey;

        // Pass 1 — product+step override.
        if (!string.IsNullOrWhiteSpace(productKey)
            && ProductStepRules.TryGetValue((productKey, stepKey), out var productRule)
            && productRule.IsAssigned)
        {
            LogDecision(instance, stepKey, productRule, "product_step");
            return productRule;
        }

        // Pass 2 — step-key default.
        if (StepRules.TryGetValue(stepKey, out var stepRule) && stepRule.IsAssigned)
        {
            LogDecision(instance, stepKey, stepRule, "step");
            return stepRule;
        }

        // Pass 3 — documented safe fallback: leave unassigned.
        LogDecision(instance, stepKey, WorkflowTaskAssignment.None, "fallback_none");
        return WorkflowTaskAssignment.None;
    }

    private void LogDecision(
        WorkflowInstance instance,
        string stepKey,
        WorkflowTaskAssignment decision,
        string source)
    {
        // Single-line, no PII. We log the assignment *type* (user /
        // role / org / none), never the resolved id values, to avoid
        // sensitive-data dumping on hot paths. The assignment itself
        // lands on the persisted task row — that is the canonical
        // surface for auditors.
        var type = decision.AssignedUserId is not null ? "user"
                 : decision.AssignedRole   is not null ? "role"
                 : decision.AssignedOrgId  is not null ? "org"
                 : "none";

        _logger.LogDebug(
            "AssignmentResolver decision instance={InstanceId} tenant={TenantId} step={StepKey} source={Source} type={Type}",
            instance.Id, instance.TenantId, stepKey, source, type);
    }

    /// <summary>
    /// Case-insensitive ordinal comparer for the
    /// <c>(ProductKey, StepKey)</c> rule key. Both members are compared
    /// independently so we get the same equality semantics as
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> on a single
    /// string.
    /// </summary>
    private sealed class ProductStepKeyComparer : IEqualityComparer<(string ProductKey, string StepKey)>
    {
        public bool Equals((string ProductKey, string StepKey) x, (string ProductKey, string StepKey) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.ProductKey, y.ProductKey)
            && StringComparer.OrdinalIgnoreCase.Equals(x.StepKey, y.StepKey);

        public int GetHashCode((string ProductKey, string StepKey) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProductKey ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.StepKey ?? string.Empty));
    }
}
