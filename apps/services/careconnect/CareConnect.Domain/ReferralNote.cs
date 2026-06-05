using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ReferralNote : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReferralId { get; private set; }

    /// <summary>Org that created this note. Determines INTERNAL note visibility.</summary>
    public Guid? OwnerOrganizationId { get; private set; }

    /// <summary>INTERNAL = only visible to OwnerOrganizationId. SHARED = all participants.</summary>
    public string VisibilityScope { get; private set; } = "SHARED";

    /// <summary>Legacy field retained during migration window. New code uses VisibilityScope.</summary>
    public bool IsInternal { get; private set; }

    public string NoteType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    public Referral? Referral { get; private set; }

    private ReferralNote() { }

    public static ReferralNote Create(
        Guid tenantId,
        Guid referralId,
        Guid? ownerOrganizationId,
        string visibilityScope,
        string noteType,
        string content,
        Guid? createdByUserId)
    {
        var scope = visibilityScope.ToUpperInvariant() == "INTERNAL" ? "INTERNAL" : "SHARED";
        var now = DateTime.UtcNow;
        return new ReferralNote
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            ReferralId          = referralId,
            OwnerOrganizationId = ownerOrganizationId,
            VisibilityScope     = scope,
            IsInternal          = scope == "INTERNAL",
            NoteType            = noteType,
            Content             = content.Trim(),
            CreatedByUserId     = createdByUserId,
            UpdatedByUserId     = createdByUserId,
            CreatedAtUtc        = now,
            UpdatedAtUtc        = now
        };
    }

    public void Update(
        string noteType,
        string content,
        string visibilityScope,
        Guid? updatedByUserId)
    {
        var scope       = visibilityScope.ToUpperInvariant() == "INTERNAL" ? "INTERNAL" : "SHARED";
        NoteType        = noteType;
        Content         = content.Trim();
        VisibilityScope = scope;
        IsInternal      = scope == "INTERNAL";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
