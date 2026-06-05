using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-019: Resolved rule pack + rules for a single evaluation context.
/// </summary>
public sealed class SmsGovernanceRuleResolution
{
    /// <summary>Effective rules in evaluation order (priority ascending).</summary>
    public IReadOnlyList<SmsGovernanceRule> Rules    { get; init; } = [];

    /// <summary>Packs that contributed to this resolution, in order.</summary>
    public IReadOnlyList<SmsGovernanceRulePack> Packs { get; init; } = [];

    /// <summary>Effective enforcement mode from profile assignment (standard = default).</summary>
    public string EnforcementMode { get; init; } = "standard";

    /// <summary>Whether a compliance profile assignment was found for the tenant.</summary>
    public bool ProfileAssigned   { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-019: Resolves the effective governance rule set for a tenant.
///
/// Resolution order:
/// 1. Load active global packs (TenantId = null, status = active, not expired).
/// 2. Load active tenant packs.
/// 3. Apply InheritanceMode:
///    - merge       — global rules + tenant rules, sorted by priority
///    - override    — tenant rules replace global rules of same RuleType
///    - append_only — global rules first, tenant rules appended
/// 4. Resolve compliance profile (tenantId → profile → packs override).
/// 5. Exclude disabled/inactive/expired rules.
/// 6. Order by priority ascending (lower = higher priority).
///
/// Resolver failures must fail open (return empty resolution — no rules applied).
/// </summary>
public interface ISmsGovernanceRuleResolver
{
    /// <summary>
    /// Resolve effective rules for the given tenant and optional context.
    /// context may be "content", "template", "escalation", or null for all.
    /// </summary>
    Task<SmsGovernanceRuleResolution> ResolveRulesAsync(
        Guid? tenantId,
        string? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resolve active rule packs only (without expanding individual rules).
    /// Used for administration / analytics.
    /// </summary>
    Task<IReadOnlyList<SmsGovernanceRulePack>> ResolveRulePacksAsync(
        Guid? tenantId,
        CancellationToken ct = default);
}
