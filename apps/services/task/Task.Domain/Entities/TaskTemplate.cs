using BuildingBlocks.Domain;
using Task.Domain.Enums;

namespace Task.Domain.Entities;

/// <summary>
/// Platform-agnostic reusable task template.
/// Null <see cref="SourceProductCode"/> means the template is tenant-wide and available to all products.
/// </summary>
public class TaskTemplate : AuditableEntity
{
    public Guid    Id                  { get; private set; }
    public Guid    TenantId            { get; private set; }
    public string? SourceProductCode   { get; private set; }

    public string  Code                { get; private set; } = string.Empty;
    public string  Name                { get; private set; } = string.Empty;
    public string? Description         { get; private set; }

    public string  DefaultTitle        { get; private set; } = string.Empty;
    public string? DefaultDescription  { get; private set; }
    public string  DefaultPriority     { get; private set; } = TaskPriority.Medium;
    public string  DefaultScope        { get; private set; } = TaskScope.General;
    public int?    DefaultDueInDays    { get; private set; }
    public Guid?   DefaultStageId      { get; private set; }

    public bool    IsActive            { get; private set; } = true;
    public int     Version             { get; private set; } = 1;

    public string? ProductSettingsJson { get; private set; }

    private TaskTemplate() { }

    public static TaskTemplate Create(
        Guid    tenantId,
        string  code,
        string  name,
        string  defaultTitle,
        Guid    createdByUserId,
        string? sourceProductCode  = null,
        string? description        = null,
        string? defaultDescription = null,
        string? defaultPriority    = null,
        string? defaultScope       = null,
        int?    defaultDueInDays   = null,
        Guid?   defaultStageId     = null,
        string? productSettingsJson = null,
        Guid?   id                 = null)
    {
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTitle);

        var effectivePriority = defaultPriority ?? TaskPriority.Medium;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(defaultPriority));

        var effectiveScope = defaultScope ?? TaskScope.General;
        if (!TaskScope.All.Contains(effectiveScope))
            throw new ArgumentException($"Invalid scope: '{effectiveScope}'.", nameof(defaultScope));

        var now = DateTime.UtcNow;
        return new TaskTemplate
        {
            Id                 = id ?? Guid.NewGuid(),
            TenantId           = tenantId,
            SourceProductCode  = sourceProductCode?.Trim().ToUpperInvariant(),
            Code               = code.Trim().ToUpperInvariant(),
            Name               = name.Trim(),
            Description        = description?.Trim(),
            DefaultTitle       = defaultTitle.Trim(),
            DefaultDescription = defaultDescription?.Trim(),
            DefaultPriority    = effectivePriority,
            DefaultScope       = effectiveScope,
            DefaultDueInDays   = defaultDueInDays,
            DefaultStageId     = defaultStageId,
            ProductSettingsJson = productSettingsJson,
            IsActive           = true,
            Version            = 1,
            CreatedByUserId    = createdByUserId,
            UpdatedByUserId    = createdByUserId,
            CreatedAtUtc       = now,
            UpdatedAtUtc       = now,
        };
    }

    public void Update(
        string  name,
        string  defaultTitle,
        Guid    updatedByUserId,
        int     expectedVersion,
        string? description         = null,
        string? defaultDescription  = null,
        string? defaultPriority     = null,
        string? defaultScope        = null,
        int?    defaultDueInDays    = null,
        Guid?   defaultStageId      = null,
        string? productSettingsJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTitle);
        if (Version != expectedVersion)
            throw new InvalidOperationException($"Version conflict — expected {Version}, got {expectedVersion}.");

        var effectivePriority = defaultPriority ?? DefaultPriority;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(defaultPriority));

        var effectiveScope = defaultScope ?? DefaultScope;
        if (!TaskScope.All.Contains(effectiveScope))
            throw new ArgumentException($"Invalid scope: '{effectiveScope}'.", nameof(defaultScope));

        Name                = name.Trim();
        Description         = description?.Trim();
        DefaultTitle        = defaultTitle.Trim();
        DefaultDescription  = defaultDescription?.Trim();
        DefaultPriority     = effectivePriority;
        DefaultScope        = effectiveScope;
        DefaultDueInDays    = defaultDueInDays;
        DefaultStageId      = defaultStageId;
        ProductSettingsJson = productSettingsJson;
        Version            += 1;
        UpdatedByUserId     = updatedByUserId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public void Activate(Guid updatedByUserId)
    {
        IsActive        = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
