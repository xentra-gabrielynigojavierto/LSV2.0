using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class Conversation : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }

    public string ProductKey { get; private set; } = string.Empty;
    public string ContextType { get; private set; } = Enums.ContextType.General;
    public string ContextId { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
    public string Status { get; private set; } = ConversationStatus.New;
    public string VisibilityType { get; private set; } = Enums.VisibilityType.InternalOnly;

    public DateTime LastActivityAtUtc { get; private set; }

    private Conversation() { }

    public static Conversation Create(
        Guid tenantId,
        Guid orgId,
        string productKey,
        string contextType,
        string contextId,
        string subject,
        string visibilityType,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(productKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextType);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (!Enums.ContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid context type: '{contextType}'.");

        if (!Enums.VisibilityType.All.Contains(visibilityType))
            throw new ArgumentException($"Invalid visibility type: '{visibilityType}'.");

        var now = DateTime.UtcNow;
        return new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrgId = orgId,
            ProductKey = productKey.Trim(),
            ContextType = contextType,
            ContextId = contextId.Trim(),
            Subject = subject.Trim(),
            Status = ConversationStatus.New,
            VisibilityType = visibilityType,
            LastActivityAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void UpdateStatus(string newStatus, Guid updatedByUserId)
    {
        if (!ConversationStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid conversation status: '{newStatus}'.");

        if (!ConversationStatus.IsValidTransition(Status, newStatus))
            throw new InvalidOperationException(
                $"Invalid status transition from '{Status}' to '{newStatus}'.");

        Status = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void TouchActivity()
    {
        LastActivityAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AutoTransitionToOpen(Guid updatedByUserId)
    {
        if (Status == ConversationStatus.New)
        {
            Status = ConversationStatus.Open;
            UpdatedByUserId = updatedByUserId;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void ReopenFromClosed(Guid updatedByUserId)
    {
        if (Status == ConversationStatus.Closed)
        {
            Status = ConversationStatus.Open;
            UpdatedByUserId = updatedByUserId;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
