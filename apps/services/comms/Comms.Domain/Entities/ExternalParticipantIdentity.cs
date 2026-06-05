using BuildingBlocks.Domain;

namespace Comms.Domain.Entities;

public class ExternalParticipantIdentity : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ExternalParticipantIdentity() { }

    public static ExternalParticipantIdentity Create(
        Guid tenantId,
        string email,
        Guid? createdByUserId,
        string? displayName = null,
        Guid? participantId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var now = DateTime.UtcNow;
        return new ExternalParticipantIdentity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NormalizedEmail = EmailMessageReference.NormalizeEmail(email),
            DisplayName = displayName?.Trim(),
            ParticipantId = participantId,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void LinkParticipant(Guid participantId, Guid? updatedByUserId)
    {
        ParticipantId = participantId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDisplayName(string? displayName, Guid? updatedByUserId)
    {
        DisplayName = displayName?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
