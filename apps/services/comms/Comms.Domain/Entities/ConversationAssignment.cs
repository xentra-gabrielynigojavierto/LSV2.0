using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class ConversationAssignment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? QueueId { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public string AssignmentStatus { get; private set; } = Enums.AssignmentStatus.Unassigned;
    public DateTime AssignedAtUtc { get; private set; }
    public DateTime LastAssignedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? UnassignedAtUtc { get; private set; }

    private ConversationAssignment() { }

    public static ConversationAssignment Create(
        Guid tenantId,
        Guid conversationId,
        Guid? queueId,
        Guid? assignedUserId,
        Guid? assignedByUserId,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));

        var now = DateTime.UtcNow;
        var status = DetermineStatus(queueId, assignedUserId);

        return new ConversationAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            QueueId = queueId,
            AssignedUserId = assignedUserId,
            AssignedByUserId = assignedByUserId,
            AssignmentStatus = status,
            AssignedAtUtc = now,
            LastAssignedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Reassign(Guid? queueId, Guid? assignedUserId, Guid? assignedByUserId, Guid updatedByUserId)
    {
        var now = DateTime.UtcNow;
        QueueId = queueId;
        AssignedUserId = assignedUserId;
        AssignedByUserId = assignedByUserId;
        AssignmentStatus = DetermineStatus(queueId, assignedUserId);
        LastAssignedAtUtc = now;
        AcceptedAtUtc = null;
        UnassignedAtUtc = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = now;
    }

    public void Accept(Guid updatedByUserId)
    {
        if (AssignedUserId is null)
            throw new InvalidOperationException("Cannot accept assignment without an assigned user.");
        if (AssignmentStatus == Enums.AssignmentStatus.Accepted)
            throw new InvalidOperationException("Assignment is already accepted.");

        var now = DateTime.UtcNow;
        AssignmentStatus = Enums.AssignmentStatus.Accepted;
        AcceptedAtUtc = now;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = now;
    }

    public void Unassign(Guid updatedByUserId)
    {
        var now = DateTime.UtcNow;
        AssignedUserId = null;
        AssignedByUserId = null;
        AssignmentStatus = QueueId.HasValue
            ? Enums.AssignmentStatus.Queued
            : Enums.AssignmentStatus.Unassigned;
        AcceptedAtUtc = null;
        UnassignedAtUtc = now;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = now;
    }

    private static string DetermineStatus(Guid? queueId, Guid? assignedUserId)
    {
        if (assignedUserId.HasValue)
            return Enums.AssignmentStatus.Assigned;
        if (queueId.HasValue)
            return Enums.AssignmentStatus.Queued;
        return Enums.AssignmentStatus.Unassigned;
    }
}
