using BuildingBlocks.Domain;

namespace Task.Domain.Entities;

/// <summary>
/// Platform-agnostic task execution stage configuration.
/// Scoped to tenant; optionally scoped to a specific product via <see cref="SourceProductCode"/>.
/// Null <see cref="SourceProductCode"/> means the stage is a tenant-wide default available to all products.
/// </summary>
public class TaskStageConfig : AuditableEntity
{
    public Guid    Id                { get; private set; }
    public Guid    TenantId          { get; private set; }
    public string? SourceProductCode { get; private set; }

    public string  Code              { get; private set; } = string.Empty;
    public string  Name              { get; private set; } = string.Empty;
    public int     DisplayOrder      { get; private set; }
    public bool    IsActive          { get; private set; } = true;

    /// <summary>
    /// Opaque JSON blob for product-specific stage extensions.
    /// For SYNQ_LIENS rows: { description, defaultOwnerRole, slaMetadata }
    /// </summary>
    public string? ProductSettingsJson { get; private set; }

    private TaskStageConfig() { }

    public static TaskStageConfig Create(
        Guid    tenantId,
        string  code,
        string  name,
        int     displayOrder,
        Guid    createdByUserId,
        string? sourceProductCode   = null,
        string? productSettingsJson = null,
        Guid?   id                  = null)
    {
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new TaskStageConfig
        {
            Id                  = id ?? Guid.NewGuid(),
            TenantId            = tenantId,
            SourceProductCode   = sourceProductCode?.Trim().ToUpperInvariant(),
            Code                = code.Trim().ToUpperInvariant(),
            Name                = name.Trim(),
            DisplayOrder        = displayOrder,
            IsActive            = true,
            ProductSettingsJson = productSettingsJson,
            CreatedByUserId     = createdByUserId,
            UpdatedByUserId     = createdByUserId,
            CreatedAtUtc        = now,
            UpdatedAtUtc        = now,
        };
    }

    public void Update(
        string  name,
        int     displayOrder,
        bool    isActive,
        Guid    updatedByUserId,
        string? productSettingsJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name                = name.Trim();
        DisplayOrder        = displayOrder;
        IsActive            = isActive;
        ProductSettingsJson = productSettingsJson;
        UpdatedByUserId     = updatedByUserId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
