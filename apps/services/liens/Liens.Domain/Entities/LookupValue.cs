using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class LookupValue : AuditableEntity
{
    public Guid Id        { get; private set; }
    public Guid? TenantId { get; private set; }

    public string Category    { get; private set; } = string.Empty;
    public string Code        { get; private set; } = string.Empty;
    public string Name        { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public int  SortOrder { get; private set; }
    public bool IsActive  { get; private set; }
    public bool IsSystem  { get; private set; }

    private LookupValue() { }

    public static LookupValue Create(
        string category,
        string code,
        string name,
        Guid createdByUserId,
        Guid? tenantId = null,
        string? description = null,
        int sortOrder = 0,
        bool isSystem = false)
    {
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!LookupCategory.All.Contains(category))
            throw new ArgumentException($"Invalid lookup category: '{category}'.");

        var now = DateTime.UtcNow;
        return new LookupValue
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Category        = category.Trim(),
            Code            = code.Trim(),
            Name            = name.Trim(),
            Description     = description?.Trim(),
            SortOrder       = sortOrder,
            IsActive        = true,
            IsSystem        = isSystem,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        string name,
        Guid updatedByUserId,
        string? description = null,
        int? sortOrder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name            = name.Trim();
        Description     = description?.Trim();
        SortOrder       = sortOrder ?? SortOrder;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        if (IsSystem)
            throw new InvalidOperationException("System lookup values cannot be deactivated.");

        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Reactivate(Guid updatedByUserId)
    {
        IsActive        = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
