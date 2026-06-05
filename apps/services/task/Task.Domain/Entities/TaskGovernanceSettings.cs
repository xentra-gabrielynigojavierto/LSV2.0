using BuildingBlocks.Domain;
using Task.Domain.Enums;

namespace Task.Domain.Entities;

/// <summary>
/// Platform-agnostic governance settings that control task behavior per tenant and,
/// optionally, per product. Null <see cref="SourceProductCode"/> means the record
/// represents the tenant-wide default.
///
/// Resolution order (applied in application layer):
///   1. Product-level settings matching <see cref="SourceProductCode"/>
///   2. Tenant-level default settings (SourceProductCode = null)
///   3. Hard-coded fallback in <see cref="GovernanceFallback"/>
/// </summary>
public class TaskGovernanceSettings : AuditableEntity
{
    public Guid    Id                          { get; private set; }
    public Guid    TenantId                    { get; private set; }
    public string? SourceProductCode           { get; private set; }

    public bool    RequireAssignee             { get; private set; } = false;
    public bool    RequireDueDate              { get; private set; } = false;
    public bool    RequireStage                { get; private set; } = false;
    public bool    AllowUnassign               { get; private set; } = true;
    public bool    AllowCancel                 { get; private set; } = true;
    public bool    AllowCompleteWithoutStage   { get; private set; } = true;
    public bool    AllowNotesOnClosedTasks     { get; private set; } = false;
    public string  DefaultPriority             { get; private set; } = TaskPriority.Medium;
    public string  DefaultTaskScope            { get; private set; } = TaskScope.General;

    public int     Version                     { get; private set; } = 1;

    /// <summary>
    /// Optional JSON blob for product-specific governance extensions that do not fit
    /// the generic model. Used by SYNQ_LIENS to store RequireCaseLinkOnCreate,
    /// AllowMultipleAssignees, DefaultStartStageMode, and ExplicitStartStageId.
    /// NULL for tenant-wide defaults and products that have no extensions.
    /// </summary>
    public string? ProductSettingsJson         { get; private set; }

    private TaskGovernanceSettings() { }

    public static TaskGovernanceSettings CreateDefault(
        Guid    tenantId,
        Guid    createdByUserId,
        string? sourceProductCode = null)
    {
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new TaskGovernanceSettings
        {
            Id                        = Guid.NewGuid(),
            TenantId                  = tenantId,
            SourceProductCode         = sourceProductCode?.Trim().ToUpperInvariant(),
            RequireAssignee           = false,
            RequireDueDate            = false,
            RequireStage              = false,
            AllowUnassign             = true,
            AllowCancel               = true,
            AllowCompleteWithoutStage = true,
            AllowNotesOnClosedTasks   = false,
            DefaultPriority           = TaskPriority.Medium,
            DefaultTaskScope          = TaskScope.General,
            Version                   = 1,
            CreatedByUserId           = createdByUserId,
            UpdatedByUserId           = createdByUserId,
            CreatedAtUtc              = now,
            UpdatedAtUtc              = now,
        };
    }

    public void Update(
        bool    requireAssignee,
        bool    requireDueDate,
        bool    requireStage,
        bool    allowUnassign,
        bool    allowCancel,
        bool    allowCompleteWithoutStage,
        bool    allowNotesOnClosedTasks,
        string  defaultPriority,
        string  defaultTaskScope,
        Guid    updatedByUserId,
        int     expectedVersion,
        string? productSettingsJson = null)
    {
        if (!TaskPriority.All.Contains(defaultPriority))
            throw new ArgumentException($"Invalid priority: '{defaultPriority}'.", nameof(defaultPriority));
        if (!TaskScope.All.Contains(defaultTaskScope))
            throw new ArgumentException($"Invalid scope: '{defaultTaskScope}'.", nameof(defaultTaskScope));
        if (Version != expectedVersion)
            throw new InvalidOperationException($"Version conflict — expected {Version}, got {expectedVersion}.");

        RequireAssignee           = requireAssignee;
        RequireDueDate            = requireDueDate;
        RequireStage              = requireStage;
        AllowUnassign             = allowUnassign;
        AllowCancel               = allowCancel;
        AllowCompleteWithoutStage = allowCompleteWithoutStage;
        AllowNotesOnClosedTasks   = allowNotesOnClosedTasks;
        DefaultPriority           = defaultPriority;
        DefaultTaskScope          = defaultTaskScope;
        ProductSettingsJson       = productSettingsJson;
        Version                  += 1;
        UpdatedByUserId           = updatedByUserId;
        UpdatedAtUtc              = DateTime.UtcNow;
    }
}

/// <summary>
/// Hard-coded fallback governance defaults used when no tenant or product settings are configured.
/// </summary>
public static class GovernanceFallback
{
    public const bool   RequireAssignee           = false;
    public const bool   RequireDueDate            = false;
    public const bool   RequireStage              = false;
    public const bool   AllowUnassign             = true;
    public const bool   AllowCancel               = true;
    public const bool   AllowCompleteWithoutStage = true;
    public const bool   AllowNotesOnClosedTasks   = false;
    public const string DefaultPriority           = TaskPriority.Medium;
    public const string DefaultTaskScope          = TaskScope.General;
}
