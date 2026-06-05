using System.Text.RegularExpressions;

namespace Identity.Domain;

public class Permission
{
    private static readonly Regex NamingConvention = new(
        @"^[A-Z0-9_]+\.[a-z][a-z0-9_]*(?:\:[a-z][a-z0-9_]*)*$",
        RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public Product Product { get; private set; } = null!;
    public ICollection<RolePermissionMapping> RolePermissionMappings { get; private set; } = [];
    public ICollection<RolePermissionAssignment> RolePermissionAssignments { get; private set; } = [];

    private Permission() { }

    public static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && NamingConvention.IsMatch(code.Trim());

    public static Permission Create(
        Guid productId,
        string code,
        string name,
        string? description = null,
        string? category = null,
        Guid? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedCode = code.Trim();
        if (!NamingConvention.IsMatch(normalizedCode))
            throw new ArgumentException(
                $"Permission code '{normalizedCode}' does not follow the naming convention '{{PRODUCT}}.{{domain}}:{{action}}' (e.g. SYNQ_FUND.application:create).",
                nameof(code));

        return new Permission
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Code = normalizedCode,
            Name = name.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    public void Update(string name, string? description, string? category, Guid? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim();
        Category = category?.Trim();
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
