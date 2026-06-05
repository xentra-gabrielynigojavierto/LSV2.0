namespace Comms.Domain.Entities;

public sealed class ConversationSlaTriggerState
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public DateTime? FirstResponseWarningSentAtUtc { get; private set; }
    public DateTime? FirstResponseBreachSentAtUtc { get; private set; }
    public DateTime? ResolutionWarningSentAtUtc { get; private set; }
    public DateTime? ResolutionBreachSentAtUtc { get; private set; }
    public DateTime? LastEvaluatedAtUtc { get; private set; }
    public Guid? LastEscalatedToUserId { get; private set; }
    public Guid? LastEscalatedQueueId { get; private set; }
    public int? WarningThresholdSnapshotMinutes { get; private set; }
    public int? EvaluationVersion { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    private ConversationSlaTriggerState() { }

    public static ConversationSlaTriggerState Create(
        Guid tenantId, Guid conversationId, Guid userId)
    {
        var now = DateTime.UtcNow;
        return new ConversationSlaTriggerState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            EvaluationVersion = 0,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedByUserId = userId,
        };
    }

    public void MarkFirstResponseWarningSent(Guid targetUserId, Guid? queueId, DateTime utcNow)
    {
        FirstResponseWarningSentAtUtc = utcNow;
        LastEscalatedToUserId = targetUserId;
        LastEscalatedQueueId = queueId;
        UpdatedAtUtc = utcNow;
    }

    public void MarkFirstResponseBreachSent(Guid targetUserId, Guid? queueId, DateTime utcNow)
    {
        FirstResponseBreachSentAtUtc = utcNow;
        LastEscalatedToUserId = targetUserId;
        LastEscalatedQueueId = queueId;
        UpdatedAtUtc = utcNow;
    }

    public void MarkResolutionWarningSent(Guid targetUserId, Guid? queueId, DateTime utcNow)
    {
        ResolutionWarningSentAtUtc = utcNow;
        LastEscalatedToUserId = targetUserId;
        LastEscalatedQueueId = queueId;
        UpdatedAtUtc = utcNow;
    }

    public void MarkResolutionBreachSent(Guid targetUserId, Guid? queueId, DateTime utcNow)
    {
        ResolutionBreachSentAtUtc = utcNow;
        LastEscalatedToUserId = targetUserId;
        LastEscalatedQueueId = queueId;
        UpdatedAtUtc = utcNow;
    }

    public void RecordEvaluation(DateTime utcNow)
    {
        LastEvaluatedAtUtc = utcNow;
        EvaluationVersion = (EvaluationVersion ?? 0) + 1;
        UpdatedAtUtc = utcNow;
    }

    public void SnapshotWarningThreshold(int minutes)
    {
        WarningThresholdSnapshotMinutes = minutes;
    }

    public bool HasFirstResponseWarningSent => FirstResponseWarningSentAtUtc.HasValue;
    public bool HasFirstResponseBreachSent => FirstResponseBreachSentAtUtc.HasValue;
    public bool HasResolutionWarningSent => ResolutionWarningSentAtUtc.HasValue;
    public bool HasResolutionBreachSent => ResolutionBreachSentAtUtc.HasValue;
}
