using System.Text.RegularExpressions;

namespace Identity.Domain;

public class Policy
{
    private static readonly Regex PolicyCodePattern = new(
        @"^[A-Z0-9_]+\.[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)*$",
        RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string PolicyCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int Priority { get; private set; }
    public PolicyEffect Effect { get; private set; } = PolicyEffect.Allow;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public ICollection<PolicyRule> Rules { get; private set; } = [];
    public ICollection<PermissionPolicy> PermissionPolicies { get; private set; } = [];

    private Policy() { }

    public static bool IsValidPolicyCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && PolicyCodePattern.IsMatch(code.Trim());

    public static Policy Create(
        string policyCode,
        string name,
        string productCode,
        string? description = null,
        int priority = 0,
        PolicyEffect effect = PolicyEffect.Allow,
        Guid? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        var normalizedCode = policyCode.Trim();
        if (!PolicyCodePattern.IsMatch(normalizedCode))
            throw new ArgumentException(
                $"Policy code '{normalizedCode}' does not follow the naming convention 'PRODUCT.domain.qualifier' (e.g. SYNQ_FUND.approval.limit).",
                nameof(policyCode));

        if (!Enum.IsDefined(effect))
            throw new ArgumentException($"Invalid policy effect '{effect}'.", nameof(effect));

        return new Policy
        {
            Id = Guid.NewGuid(),
            PolicyCode = normalizedCode,
            Name = name.Trim(),
            Description = description?.Trim(),
            ProductCode = productCode.Trim(),
            IsActive = true,
            Priority = priority,
            Effect = effect,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    public void Update(string name, string? description, int priority, PolicyEffect? effect = null, Guid? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim();
        Priority = priority;
        if (effect.HasValue && Enum.IsDefined(effect.Value))
            Effect = effect.Value;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Deactivate(Guid? updatedBy = null)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Activate(Guid? updatedBy = null)
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
